using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VulnX.Application.Abstractions;
using VulnX.Application.Common;
using VulnX.Application.Scans;
using VulnX.Domain.Entities;
using VulnX.Domain.Enums;
using VulnX.Infrastructure.Persistence;
using VulnX.Scanner.Engine;

namespace VulnX.Infrastructure.Scans;

/// Runs queued scans in the background (max 2 at the same time).
internal sealed class ScanWorker(IServiceScopeFactory scopes, IScanQueue queue, ILogger<ScanWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverAsync(stoppingToken);
        try
        {
            await Parallel.ForEachAsync(queue.DequeueAllAsync(stoppingToken),
                new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = stoppingToken },
                async (scanId, ct) =>
                {
                    try { await ProcessAsync(scanId, ct); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex, "Scan {ScanId} crashed", scanId);
                    }
                });
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    /// After a restart: running scans are marked failed, queued scans are queued again.
    private async Task RecoverAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var stale = await db.Scans.Where(s => s.Status == ScanStatus.Running).ToListAsync(ct);
            foreach (var s in stale)
            {
                s.Status = ScanStatus.Failed;
                s.Error = "Interrupted by a server restart.";
                s.CompletedAtUtc = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);

            var queued = await db.Scans.Where(s => s.Status == ScanStatus.Queued)
                .OrderBy(s => s.CreatedAtUtc).Select(s => s.Id).ToListAsync(ct);
            foreach (var id in queued) await queue.EnqueueAsync(id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Scan recovery failed");
        }
    }

    private async Task ProcessAsync(Guid scanId, CancellationToken stop)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var engine = sp.GetRequiredService<IScanEngine>();
        var validator = sp.GetRequiredService<ITargetValidator>();
        var notifier = sp.GetRequiredService<IScanProgressNotifier>();

        var scan = await db.Scans.Include(s => s.Target).Include(s => s.Modules)
            .FirstOrDefaultAsync(s => s.Id == scanId, stop);
        if (scan is null || scan.Status != ScanStatus.Queued) return;   // cancelled while waiting

        var cts = queue.Register(scanId, stop);
        var findingsCount = 0;

        async Task Publish()
        {
            try { await notifier.PublishAsync(ScanMapper.Progress(scan, findingsCount)); }
            catch (Exception ex) { logger.LogWarning(ex, "Could not publish progress for scan {ScanId}", scanId); }
        }

        async Task OnModuleAsync(string name, ModuleStatus status, int overall, string? error)
        {
            var module = scan.Modules.FirstOrDefault(m => m.ModuleName == name);
            if (module is not null)
            {
                module.Status = status;
                module.Error = error;
                if (status == ModuleStatus.Running) { module.StartedAtUtc = DateTime.UtcNow; module.ProgressPercent = 10; }
                else { module.CompletedAtUtc = DateTime.UtcNow; module.ProgressPercent = 100; }
            }
            scan.ProgressPercent = overall;
            await db.SaveChangesAsync(cts.Token);
            await Publish();
        }

        void Finish(ScanStatus status, string? error)
        {
            scan.Status = status;
            scan.Error = error;
            scan.CompletedAtUtc = DateTime.UtcNow;
            foreach (var m in scan.Modules.Where(m => m.Status is ModuleStatus.Pending or ModuleStatus.Running))
                m.Status = ModuleStatus.Skipped;
        }

        scan.Status = ScanStatus.Running;
        scan.StartedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(stop);
        await Publish();

        try
        {
            // Validate again now: DNS may have changed since the scan was queued.
            var target = await validator.ValidateAsync(scan.Target.NormalizedUrl, cts.Token);
            var outcome = await engine.RunAsync(target, scan.Profile, OnModuleAsync, cts.Token);

            var distinct = outcome.Findings
                .GroupBy(f => (f.Code, f.AffectedUrl, f.Evidence)).Select(g => g.First()).ToList();
            var codes = distinct.Select(f => f.Code).Distinct().ToList();
            var definitions = await db.VulnerabilityDefinitions.Where(d => codes.Contains(d.Code))
                .ToDictionaryAsync(d => d.Code, cts.Token);

            foreach (var f in distinct)
            {
                var entry = VulnerabilityCatalog.Get(f.Code);
                if (!definitions.TryGetValue(f.Code, out var definition))
                {
                    definition = new VulnerabilityDefinition
                    {
                        Code = entry.Code,
                        Name = entry.Name,
                        Category = entry.Category,
                        Description = entry.Description,
                        DefaultSeverity = entry.Severity,
                        References = entry.References.ToList()
                    };
                    db.VulnerabilityDefinitions.Add(definition);
                    definitions[f.Code] = definition;
                }

                db.ScanResults.Add(new ScanResult
                {
                    ScanId = scan.Id,
                    VulnerabilityDefinitionId = definition.Id,
                    Category = entry.Category,
                    Title = entry.Name,
                    Severity = f.SeverityOverride ?? entry.Severity,
                    Confidence = f.Confidence,
                    Description = entry.Description,
                    RootCause = entry.RootCause,
                    Impact = entry.Impact,
                    AffectedUrl = f.AffectedUrl,
                    Evidence = f.Evidence,
                    Recommendation = entry.Recommendation,
                    References = entry.References.ToList(),
                    DetectionModule = f.Module
                });
            }

            var worstPerCode = distinct.GroupBy(f => f.Code)
                .Select(g => g.Max(f => f.SeverityOverride ?? VulnerabilityCatalog.Get(f.Code).Severity));
            scan.Score = ScoreCalculator.Calculate(worstPerCode);   // deterministic, never set by AI
            findingsCount = distinct.Count;
            scan.ProgressPercent = 100;
            Finish(ScanStatus.Completed, null);
        }
        catch (OperationCanceledException) when (!stop.IsCancellationRequested)
        {
            Finish(ScanStatus.Cancelled, null);
        }
        catch (OperationCanceledException)
        {
            Finish(ScanStatus.Failed, "Interrupted by server shutdown.");
        }
        catch (ScanTargetUnreachableException ex)
        {
            Finish(ScanStatus.Failed, ex.Message);
        }
        catch (AppException ex)
        {
            Finish(ScanStatus.Failed, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scan {ScanId} failed unexpectedly", scanId);
            Finish(ScanStatus.Failed, "An unexpected error occurred while scanning.");
        }
        finally
        {
            queue.Unregister(scanId);
        }

        await db.SaveChangesAsync(CancellationToken.None);

        try
        {
            await sp.GetRequiredService<INotificationService>().NotifyScanFinishedAsync(scan.Id, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not create the notification for scan {ScanId}", scanId);
        }
        await Publish();
    }
}
