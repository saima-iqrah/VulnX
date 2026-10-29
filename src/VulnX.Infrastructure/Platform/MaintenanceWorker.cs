using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VulnX.Infrastructure.Persistence;

namespace VulnX.Infrastructure.Platform;

/// Daily housekeeping: removes old refresh tokens, audit logs and notifications.
internal sealed class MaintenanceWorker(IServiceScopeFactory scopes, ILogger<MaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);
            try { await Task.Delay(TimeSpan.FromHours(24), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;

            var tokenCutoffExpired = now.AddDays(-7);
            var tokenCutoffRevoked = now.AddDays(-30);
            var tokens = await db.RefreshTokens
                .Where(t => t.ExpiresAtUtc < tokenCutoffExpired || (t.RevokedAtUtc != null && t.RevokedAtUtc < tokenCutoffRevoked))
                .ExecuteDeleteAsync(ct);

            var auditCutoff = now.AddDays(-365);
            var audits = await db.AuditLogs.Where(a => a.CreatedAtUtc < auditCutoff).ExecuteDeleteAsync(ct);

            var readCutoff = now.AddDays(-60);
            var anyCutoff = now.AddDays(-180);
            var notes = await db.Notifications
                .Where(n => (n.ReadAtUtc != null && n.ReadAtUtc < readCutoff) || n.CreatedAtUtc < anyCutoff)
                .ExecuteDeleteAsync(ct);

            logger.LogInformation("Maintenance finished: {Tokens} tokens, {Audits} audit logs, {Notes} notifications removed", tokens, audits, notes);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Maintenance run failed");
        }
    }
}