using Microsoft.EntityFrameworkCore;
using VulnX.Application.Abstractions;
using VulnX.Application.Common;
using VulnX.Application.Scans;
using VulnX.Domain.Entities;
using VulnX.Domain.Enums;
using VulnX.Infrastructure.Persistence;
using VulnX.Scanner.Engine;

namespace VulnX.Infrastructure.Scans;

internal static class ScanMapper
{
    public static string? Grade(int? score) => score is int s ? ScoreCalculator.GradeFor(s) : null;

    private static List<ScanModuleDto> Modules(Scan s)
    {
        var order = ScanProfiles.For(s.Profile).Modules;
        return s.Modules
            .OrderBy(m => Array.IndexOf(order, m.ModuleName))
            .Select(m => new ScanModuleDto(m.ModuleName, m.Status.ToString(), m.ProgressPercent, m.Error))
            .ToList();
    }

    public static ScanSummaryDto Summary(Scan s, string target, int findings, IReadOnlyList<string> tags) =>
        new(s.Id, target, s.Profile.ToString(), s.Status.ToString(), s.ProgressPercent, s.Score, Grade(s.Score),
            findings, s.Label, tags, s.CreatedAtUtc, s.CompletedAtUtc);

    public static ScanProgressDto Progress(Scan s, int findings) =>
        new(s.Id, s.Status.ToString(), s.ProgressPercent,
            s.Modules.FirstOrDefault(m => m.Status == ModuleStatus.Running)?.ModuleName,
            Modules(s), findings, s.Score, Grade(s.Score), s.Error);

    public static ScanDetailDto Detail(Scan s) =>
        new(s.Id, s.Target.NormalizedUrl, s.Profile.ToString(), s.Status.ToString(), s.ProgressPercent, s.Score,
            Grade(s.Score), s.Error, s.Label, s.Notes, s.Tags.Select(t => t.Name).OrderBy(n => n).ToList(),
            s.CreatedAtUtc, s.StartedAtUtc, s.CompletedAtUtc, Modules(s),
            s.Results.OrderByDescending(r => r.Severity).ThenBy(r => r.Definition.Code)
                .Select(r => new ScanResultDto(r.Id, r.Definition.Code, r.Title, r.Category, r.Severity.ToString(),
                    r.Confidence.ToString(), r.Description, r.RootCause, r.Impact, r.AffectedUrl, r.Evidence,
                    r.Recommendation, r.References, r.DetectionModule, r.DetectedAtUtc))
                .ToList());
}

internal sealed class ScanService(
    AppDbContext db,
    ITargetValidator validator,
    IScanQueue queue,
    IScanProgressNotifier notifier,
    IAuditLogger audit,
    ISystemSettings settings) : IScanService
{
    private const int MaxActiveScans = 2;
    private const int MaxScansPerHour = 10;

    public async Task<ScanSummaryDto> CreateAsync(Guid userId, CreateScanRequest request, CancellationToken ct)
    {
        if (!request.AuthorizationConfirmed)
            throw new AppException(400, "You must confirm that you are authorized to test this target.");
        if (!Enum.TryParse<ScanProfile>(request.Profile, true, out var profile) || !Enum.IsDefined(profile))
            throw new AppException(400, "Unknown scan profile.");

        var target = await validator.ValidateAsync(request.Url, ct);   // SSRF + scope checks

        var active = await db.Scans.CountAsync(s => s.UserId == userId &&
            (s.Status == ScanStatus.Queued || s.Status == ScanStatus.Running), ct);
        if (active >= await settings.GetIntAsync("scan.maxActive", 2, ct))
            throw new AppException(429, "You already have " + active + " scans in progress. Wait for one to finish.");

        var since = DateTime.UtcNow.AddHours(-1);
        if (await db.Scans.CountAsync(s => s.UserId == userId && s.CreatedAtUtc >= since, ct) >= await settings.GetIntAsync("scan.maxPerHour", 10, ct))
            throw new AppException(429, "Scan limit reached (10 per hour). Please try again later.");

        var normalized = target.Url.ToString();
        var scanTarget = await db.ScanTargets.FirstOrDefaultAsync(t => t.UserId == userId && t.NormalizedUrl == normalized, ct);
        if (scanTarget is null)
        {
            scanTarget = new ScanTarget { UserId = userId, NormalizedUrl = normalized, Hostname = target.Hostname };
            db.ScanTargets.Add(scanTarget);
        }

        var scan = new Scan
        {
            UserId = userId,
            Target = scanTarget,
            Profile = profile,
            Status = ScanStatus.Queued,
            AuthorizationConfirmedAtUtc = DateTime.UtcNow
        };
        foreach (var module in ScanProfiles.For(profile).Modules)
            scan.Modules.Add(new ScanModule { ModuleName = module });

        db.Scans.Add(scan);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(userId, "scan.created", "Scan", scan.Id.ToString(),
            new { host = target.Hostname, profile = profile.ToString() }, ct);
        await queue.EnqueueAsync(scan.Id);

        return ScanMapper.Summary(scan, normalized, 0, Array.Empty<string>());
    }

    public async Task<PagedResult<ScanSummaryDto>> ListAsync(Guid userId, int page, int pageSize,
        string? status, string? search, string? tag, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = db.Scans.Where(s => s.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ScanStatus>(status, true, out var st))
            query = query.Where(s => s.Status == st);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(s => s.Target.NormalizedUrl.ToLower().Contains(term) ||
                                     (s.Label != null && s.Label.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var tagName = tag.Trim();
            query = query.Where(s => s.Tags.Any(t => t.Name == tagName));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new
            {
                Scan = s,
                Target = s.Target.NormalizedUrl,
                Findings = s.Results.Count,
                Tags = s.Tags.OrderBy(t => t.Name).Select(t => t.Name).ToList()
            })
            .ToListAsync(ct);

        var items = rows.Select(r => ScanMapper.Summary(r.Scan, r.Target, r.Findings, r.Tags)).ToList();
        return new PagedResult<ScanSummaryDto>(items, total, page, pageSize);
    }

    public async Task<ScanDetailDto> GetAsync(Guid userId, Guid scanId, CancellationToken ct)
    {
        var scan = await db.Scans.AsNoTracking()
            .Include(s => s.Target)
            .Include(s => s.Modules)
            .Include(s => s.Tags)
            .Include(s => s.Results).ThenInclude(r => r.Definition)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == scanId && s.UserId == userId, ct)
            ?? throw new AppException(404, "Scan not found.");
        return ScanMapper.Detail(scan);
    }

    public async Task<ScanDetailDto> UpdateAsync(Guid userId, Guid scanId, UpdateScanRequest request, CancellationToken ct)
    {
        var scan = await db.Scans.Include(s => s.Tags)
            .FirstOrDefaultAsync(s => s.Id == scanId && s.UserId == userId, ct)
            ?? throw new AppException(404, "Scan not found.");

        if (request.Label is not null)
            scan.Label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim();
        if (request.Notes is not null)
            scan.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        if (request.Tags is not null)
        {
            var names = request.Tags
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .Select(t => t.Length > 40 ? t[..40] : t)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();

            var existing = await db.Tags.Where(t => t.UserId == userId).ToListAsync(ct);
            scan.Tags.Clear();
            foreach (var name in names)
            {
                var tag = existing.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
                if (tag is null)
                {
                    tag = new Tag { UserId = userId, Name = name };
                    db.Tags.Add(tag);   // explicit Add, otherwise EF may treat a new Guid key as an existing row
                    existing.Add(tag);
                }
                scan.Tags.Add(tag);
            }
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(userId, "scan.updated", "Scan", scanId.ToString(), null, ct);
        return await GetAsync(userId, scanId, ct);
    }

    public async Task DeleteAsync(Guid userId, Guid scanId, CancellationToken ct)
    {
        var scan = await db.Scans.FirstOrDefaultAsync(s => s.Id == scanId && s.UserId == userId, ct)
            ?? throw new AppException(404, "Scan not found.");

        if (scan.Status is ScanStatus.Queued or ScanStatus.Running)
            throw new AppException(409, "Cancel the scan before deleting it.");

        db.Scans.Remove(scan);   // modules, findings, tags links and AI chats are removed by cascade
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(userId, "scan.deleted", "Scan", scanId.ToString(), null, ct);
    }

    public async Task CancelAsync(Guid userId, Guid scanId, CancellationToken ct)
    {
        var scan = await db.Scans.Include(s => s.Modules)
            .FirstOrDefaultAsync(s => s.Id == scanId && s.UserId == userId, ct)
            ?? throw new AppException(404, "Scan not found.");

        if (scan.Status is ScanStatus.Completed or ScanStatus.Failed or ScanStatus.Cancelled)
            throw new AppException(409, "This scan has already finished.");

        if (scan.Status == ScanStatus.Queued)
        {
            scan.Status = ScanStatus.Cancelled;
            scan.CompletedAtUtc = DateTime.UtcNow;
            foreach (var m in scan.Modules) m.Status = ModuleStatus.Skipped;
            await db.SaveChangesAsync(ct);
            await notifier.PublishAsync(ScanMapper.Progress(scan, 0), ct);
        }
        else
        {
            queue.RequestCancel(scanId);   // the worker stops the running scan and publishes the final state
        }

        await audit.LogAsync(userId, "scan.cancelled", "Scan", scanId.ToString(), null, ct);
    }

    public async Task<ScanCompareDto> CompareAsync(Guid userId, Guid baseId, Guid targetId, CancellationToken ct)
    {
        if (baseId == targetId) throw new AppException(400, "Choose two different scans to compare.");

        var scans = await db.Scans.AsNoTracking()
            .Include(s => s.Target)
            .Include(s => s.Tags)
            .Include(s => s.Results).ThenInclude(r => r.Definition)
            .AsSplitQuery()
            .Where(s => s.UserId == userId && (s.Id == baseId || s.Id == targetId))
            .ToListAsync(ct);

        var b = scans.FirstOrDefault(s => s.Id == baseId) ?? throw new AppException(404, "Base scan not found.");
        var t = scans.FirstOrDefault(s => s.Id == targetId) ?? throw new AppException(404, "Target scan not found.");
        if (b.Status != ScanStatus.Completed || t.Status != ScanStatus.Completed)
            throw new AppException(409, "Both scans must be completed to compare them.");

        // The same finding is identified by its code and URL (cookie findings also by their evidence = cookie name).
        static string Key(ScanResult r) =>
            r.Definition.Code + "|" + r.AffectedUrl + "|" + (r.Definition.Code.StartsWith("CKE", StringComparison.Ordinal) ? r.Evidence : "");

        var baseMap = b.Results.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());
        var targetMap = t.Results.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());

        static FindingKeyDto ToDto(ScanResult r) =>
            new(r.Definition.Code, r.Title, r.Severity.ToString(), r.AffectedUrl, r.Evidence);
        static List<FindingKeyDto> Sorted(IEnumerable<ScanResult> rs) =>
            rs.OrderByDescending(r => r.Severity).ThenBy(r => r.Definition.Code).Select(ToDto).ToList();

        var added = Sorted(targetMap.Where(kv => !baseMap.ContainsKey(kv.Key)).Select(kv => kv.Value));
        var resolved = Sorted(baseMap.Where(kv => !targetMap.ContainsKey(kv.Key)).Select(kv => kv.Value));
        var unchanged = Sorted(targetMap.Where(kv => baseMap.ContainsKey(kv.Key)).Select(kv => kv.Value));

        static IReadOnlyList<string> TagNames(Scan s) => s.Tags.Select(x => x.Name).OrderBy(n => n).ToList();

        return new ScanCompareDto(
            ScanMapper.Summary(b, b.Target.NormalizedUrl, b.Results.Count, TagNames(b)),
            ScanMapper.Summary(t, t.Target.NormalizedUrl, t.Results.Count, TagNames(t)),
            t.Score is int ts && b.Score is int bs ? ts - bs : null,
            added, resolved, unchanged);
    }

    public async Task<DashboardDto> DashboardAsync(Guid userId, CancellationToken ct)
    {
        var total = await db.Scans.CountAsync(s => s.UserId == userId, ct);
        var completedQuery = db.Scans.Where(s => s.UserId == userId && s.Status == ScanStatus.Completed);
        var completed = await completedQuery.CountAsync(ct);

        var latest = await completedQuery
            .OrderByDescending(s => s.CompletedAtUtc)
            .Select(s => new { s.Id, s.Score })
            .FirstOrDefaultAsync(ct);

        var severityCounts = new Dictionary<string, int>();
        foreach (var name in Enum.GetNames<Severity>()) severityCounts[name] = 0;

        if (latest is not null)
        {
            var grouped = await db.ScanResults
                .Where(r => r.ScanId == latest.Id)
                .GroupBy(r => r.Severity)
                .Select(g => new { Severity = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            foreach (var g in grouped) severityCounts[g.Severity.ToString()] = g.Count;
        }

        var recentCompleted = await completedQuery
            .OrderByDescending(s => s.CompletedAtUtc)
            .Take(10)
            .Select(s => new
            {
                s.Id,
                s.CompletedAtUtc,
                s.Score,
                Critical = s.Results.Count(r => r.Severity == Severity.Critical),
                High = s.Results.Count(r => r.Severity == Severity.High),
                Medium = s.Results.Count(r => r.Severity == Severity.Medium),
                Low = s.Results.Count(r => r.Severity == Severity.Low)
            })
            .ToListAsync(ct);

        var trend = recentCompleted
            .OrderBy(x => x.CompletedAtUtc)
            .Select(x => new TrendPointDto(x.Id, x.CompletedAtUtc ?? DateTime.UtcNow, x.Score ?? 0, x.Critical, x.High, x.Medium, x.Low))
            .ToList();

        var recent = (await ListAsync(userId, 1, 5, null, null, null, ct)).Items;

        return new DashboardDto(
            total, completed, latest?.Score, ScanMapper.Grade(latest?.Score),
            severityCounts.Values.Sum(), severityCounts["Critical"], severityCounts, trend, recent);
    }

    public Task<bool> OwnsAsync(Guid userId, Guid scanId, CancellationToken ct) =>
        db.Scans.AnyAsync(s => s.Id == scanId && s.UserId == userId, ct);
}


