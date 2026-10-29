using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VulnX.Application.Abstractions;
using VulnX.Application.Common;
using VulnX.Application.Platform;
using VulnX.Application.Scans;
using VulnX.Domain.Entities;
using VulnX.Domain.Enums;
using VulnX.Infrastructure.Identity;
using VulnX.Infrastructure.Persistence;

namespace VulnX.Infrastructure.Platform;

internal sealed class NotificationService(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    IAppEmailSender email,
    IConfiguration config,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task NotifyScanFinishedAsync(Guid scanId, CancellationToken ct = default)
    {
        var scan = await db.Scans.AsNoTracking().Include(s => s.Target).Include(s => s.Results)
            .FirstOrDefaultAsync(s => s.Id == scanId, ct);
        if (scan is null || scan.Status is ScanStatus.Cancelled or ScanStatus.Queued or ScanStatus.Running) return;

        var host = scan.Target.Hostname;

        if (scan.Status == ScanStatus.Completed)
        {
            var grade = scan.Score is int s ? ScoreCalculator.GradeFor(s) : "-";
            db.Notifications.Add(new Notification
            {
                UserId = scan.UserId,
                Type = NotificationType.ScanCompleted,
                Title = $"Scan finished: {host}",
                Message = $"Grade {grade} ({scan.Score}/100) with {scan.Results.Count} finding(s)."
            });

            var critical = scan.Results.Count(r => r.Severity == Severity.Critical);
            var high = scan.Results.Count(r => r.Severity == Severity.High);
            if (critical + high > 0)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = scan.UserId,
                    Type = NotificationType.CriticalFinding,
                    Title = $"Serious findings on {host}",
                    Message = $"{critical} critical and {high} high severity finding(s) need attention."
                });
            }

            await db.SaveChangesAsync(ct);
            if (critical > 0) await TryEmailAsync(scan, host, critical, ct);
        }
        else
        {
            db.Notifications.Add(new Notification
            {
                UserId = scan.UserId,
                Type = NotificationType.ScanCompleted,
                Title = $"Scan failed: {host}",
                Message = scan.Error ?? "The scan could not be completed."
            });
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task TryEmailAsync(Scan scan, string host, int critical, CancellationToken ct)
    {
        try
        {
            var user = await users.FindByIdAsync(scan.UserId.ToString());
            if (string.IsNullOrWhiteSpace(user?.Email)) return;

            var baseUrl = (config["Frontend:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
            var safeHost = System.Net.WebUtility.HtmlEncode(host);
            await email.SendAsync(user.Email, $"VulnX: {critical} critical finding(s) on {host}",
                $"<p>The scan of <strong>{safeHost}</strong> found {critical} critical finding(s).</p>" +
                $"<p><a href=\"{baseUrl}/app/scans/{scan.Id}\">Open the scan</a></p>", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not send the critical-finding email");
        }
    }

    public async Task<PagedResult<NotificationDto>> ListAsync(Guid userId, int page, int pageSize, bool unreadOnly, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => n.ReadAtUtc == null);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = rows.Select(n => new NotificationDto(n.Id, n.Type.ToString(), n.Title, n.Message, n.CreatedAtUtc, n.ReadAtUtc != null)).ToList();
        return new PagedResult<NotificationDto>(items, total, page, pageSize);
    }

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken ct) =>
        db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAtUtc == null, ct);

    public async Task MarkReadAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
                ?? throw new AppException(404, "Notification not found.");
        if (n.ReadAtUtc is null)
        {
            n.ReadAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public Task MarkAllReadAsync(Guid userId, CancellationToken ct) =>
        db.Notifications.Where(n => n.UserId == userId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, (DateTime?)DateTime.UtcNow), ct);

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var count = await db.Notifications.Where(n => n.Id == id && n.UserId == userId).ExecuteDeleteAsync(ct);
        if (count == 0) throw new AppException(404, "Notification not found.");
    }

    public async Task<int> AnnounceAsync(string title, string message, CancellationToken ct)
    {
        var ids = await db.Users.Where(u => u.Status == AccountStatus.Active).Select(u => u.Id).ToListAsync(ct);
        foreach (var id in ids)
            db.Notifications.Add(new Notification { UserId = id, Type = NotificationType.Announcement, Title = title.Trim(), Message = message.Trim() });
        await db.SaveChangesAsync(ct);
        return ids.Count;
    }
}