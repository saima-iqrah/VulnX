using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using VulnX.Application.Abstractions;
using VulnX.Application.Common;
using VulnX.Application.Platform;
using VulnX.Application.Scans;
using VulnX.Domain.Common;
using VulnX.Domain.Enums;
using VulnX.Infrastructure.Identity;
using VulnX.Infrastructure.Persistence;

namespace VulnX.Infrastructure.Platform;

internal sealed class AdminService(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    ISystemSettings settings,
    INotificationService notifications,
    IAiAssistant ai,
    IAuditLogger audit,
    IConfiguration config,
    IHostEnvironment env) : IAdminService
{
    private Task<Guid> AdminRoleIdAsync(CancellationToken ct) =>
        db.Roles.Where(r => r.Name == Roles.Admin).Select(r => r.Id).FirstOrDefaultAsync(ct);

    private async Task<ApplicationUser> GetUserAsync(Guid id) =>
        await users.FindByIdAsync(id.ToString()) ?? throw new AppException(404, "User not found.");

    private Task RevokeSessionsAsync(Guid userId, CancellationToken ct) =>
        db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, (DateTime?)DateTime.UtcNow), ct);

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new AppException(400, string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    // ---------- overview + health ----------
    public async Task<AdminOverviewDto> OverviewAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var day = now.AddHours(-24);
        var week = now.AddDays(-7);
        var adminRoleId = await AdminRoleIdAsync(ct);

        var since = now.Date.AddDays(-6);
        var daily = await db.Scans.Where(s => s.CreatedAtUtc >= since)
            .GroupBy(s => s.CreatedAtUtc.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var perDay = Enumerable.Range(0, 7).Select(i => since.AddDays(i))
            .Select(d => new DailyCountDto(d, daily.FirstOrDefault(x => x.Date.Date == d.Date)?.Count ?? 0))
            .ToList();

        return new AdminOverviewDto(
            await db.Users.CountAsync(ct),
            await db.Users.CountAsync(u => u.Status != AccountStatus.Active, ct),
            await db.UserRoles.CountAsync(ur => ur.RoleId == adminRoleId, ct),
            await db.Users.CountAsync(u => u.CreatedAtUtc >= week, ct),
            await db.Scans.CountAsync(ct),
            await db.Scans.CountAsync(s => s.CreatedAtUtc >= day, ct),
            await db.Scans.CountAsync(s => s.Status == ScanStatus.Running, ct),
            await db.Scans.CountAsync(s => s.Status == ScanStatus.Failed && s.CreatedAtUtc >= day, ct),
            await db.ScanResults.CountAsync(r => r.Severity == Severity.Critical, ct),
            await db.AuditLogs.CountAsync(a => a.CreatedAtUtc >= day, ct),
            perDay);
    }

    public async Task<SystemHealthDto> HealthAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var dbOk = false;
        try { dbOk = await db.Database.CanConnectAsync(ct); } catch { dbOk = false; }
        sw.Stop();

        var queued = 0;
        var running = 0;
        DateTime? last = null;
        if (dbOk)
        {
            queued = await db.Scans.CountAsync(s => s.Status == ScanStatus.Queued, ct);
            running = await db.Scans.CountAsync(s => s.Status == ScanStatus.Running, ct);
            last = await db.Scans.Where(s => s.Status == ScanStatus.Completed)
                .OrderByDescending(s => s.CompletedAtUtc).Select(s => s.CompletedAtUtc).FirstOrDefaultAsync(ct);
        }

        using var proc = Process.GetCurrentProcess();
        return new SystemHealthDto(
            dbOk ? "Healthy" : "Unhealthy", dbOk, sw.ElapsedMilliseconds, queued, running, last,
            ai.IsAiConfigured, !string.IsNullOrWhiteSpace(config["Smtp:Host"]), env.EnvironmentName,
            typeof(AdminService).Assembly.GetName().Version?.ToString() ?? "1.0",
            (long)(DateTime.UtcNow - proc.StartTime.ToUniversalTime()).TotalSeconds,
            proc.WorkingSet64 / (1024 * 1024), DateTime.UtcNow);
    }

    // ---------- users ----------
    public async Task<PagedResult<AdminUserDto>> ListUsersAsync(string? search, string? status, string? role, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var adminRoleId = await AdminRoleIdAsync(ct);

        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u => (u.Email != null && u.Email.ToLower().Contains(term)) || u.FullName.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AccountStatus>(status, true, out var st))
            query = query.Where(u => u.Status == st);
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == adminRoleId));
        else if (string.Equals(role, "User", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => !db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == adminRoleId));

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(u => u.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FullName,
                u.Status,
                u.EmailConfirmed,
                u.CreatedAtUtc,
                u.LastLoginAtUtc,
                IsAdmin = db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == adminRoleId),
                Scans = db.Scans.Count(s => s.UserId == u.Id)
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new AdminUserDto(r.Id, r.Email ?? "", r.FullName, r.Status.ToString(), r.IsAdmin,
            r.EmailConfirmed, r.Scans, r.CreatedAtUtc, r.LastLoginAtUtc)).ToList();
        return new PagedResult<AdminUserDto>(items, total, page, pageSize);
    }

    public async Task SetStatusAsync(Guid adminId, Guid userId, string status, CancellationToken ct)
    {
        if (adminId == userId) throw new AppException(400, "You cannot change your own account status.");
        if (!Enum.TryParse<AccountStatus>(status, true, out var st) || !Enum.IsDefined(st))
            throw new AppException(400, "Unknown status.");

        var user = await GetUserAsync(userId);
        user.Status = st;
        Check(await users.UpdateAsync(user));

        if (st == AccountStatus.Active)
        {
            await users.SetLockoutEndDateAsync(user, null);
            await users.ResetAccessFailedCountAsync(user);
        }
        else
        {
            await RevokeSessionsAsync(userId, ct);
        }

        await audit.LogAsync(adminId, "admin.user_status_changed", "User", userId.ToString(), new { status = st.ToString() }, ct);
    }

    public async Task SetRoleAsync(Guid adminId, Guid userId, bool makeAdmin, CancellationToken ct)
    {
        if (adminId == userId) throw new AppException(400, "You cannot change your own role.");

        var user = await GetUserAsync(userId);
        var isAdmin = await users.IsInRoleAsync(user, Roles.Admin);
        if (makeAdmin == isAdmin) return;

        if (!makeAdmin && (await users.GetUsersInRoleAsync(Roles.Admin)).Count <= 1)
            throw new AppException(409, "At least one administrator must remain.");

        await users.RemoveFromRoleAsync(user, makeAdmin ? Roles.User : Roles.Admin);
        Check(await users.AddToRoleAsync(user, makeAdmin ? Roles.Admin : Roles.User));
        await RevokeSessionsAsync(userId, ct);   // the new role is picked up at the next sign-in

        await audit.LogAsync(adminId, "admin.user_role_changed", "User", userId.ToString(), new { admin = makeAdmin }, ct);
    }

    public async Task DeleteUserAsync(Guid adminId, Guid userId, CancellationToken ct)
    {
        if (adminId == userId) throw new AppException(400, "You cannot delete your own account.");

        var user = await GetUserAsync(userId);
        if (await users.IsInRoleAsync(user, Roles.Admin) && (await users.GetUsersInRoleAsync(Roles.Admin)).Count <= 1)
            throw new AppException(409, "At least one administrator must remain.");

        Check(await users.DeleteAsync(user));   // scans, findings, tokens, notifications are removed by cascade
        await audit.LogAsync(adminId, "admin.user_deleted", "User", userId.ToString(), null, ct);
    }

    // ---------- scans ----------
    public async Task<PagedResult<AdminScanDto>> ListScansAsync(string? search, string? status, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = from s in db.Scans.AsNoTracking()
                    join u in db.Users on s.UserId equals u.Id
                    select new
                    {
                        s.Id,
                        Target = s.Target.NormalizedUrl,
                        Owner = u.Email,
                        s.Profile,
                        s.Status,
                        s.Score,
                        s.Label,
                        s.CreatedAtUtc,
                        Findings = s.Results.Count
                    };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.Target.ToLower().Contains(term)
                                     || (x.Owner != null && x.Owner.ToLower().Contains(term))
                                     || (x.Label != null && x.Label.ToLower().Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ScanStatus>(status, true, out var st))
            query = query.Where(x => x.Status == st);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = rows.Select(x => new AdminScanDto(x.Id, x.Target, x.Owner ?? "", x.Profile.ToString(), x.Status.ToString(),
            x.Score, x.Score is int sc ? ScoreCalculator.GradeFor(sc) : null, x.Findings, x.Label, x.CreatedAtUtc)).ToList();
        return new PagedResult<AdminScanDto>(items, total, page, pageSize);
    }

    public async Task DeleteScanAsync(Guid adminId, Guid scanId, CancellationToken ct)
    {
        var scan = await db.Scans.FirstOrDefaultAsync(s => s.Id == scanId, ct) ?? throw new AppException(404, "Scan not found.");
        if (scan.Status is ScanStatus.Queued or ScanStatus.Running)
            throw new AppException(409, "A running scan cannot be deleted. Wait until it finishes.");

        db.Scans.Remove(scan);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(adminId, "admin.scan_deleted", "Scan", scanId.ToString(), null, ct);
    }

    // ---------- audit ----------
    public async Task<PagedResult<AuditLogDto>> ListAuditAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = from a in db.AuditLogs.AsNoTracking()
                    join u in db.Users on a.UserId equals (Guid?)u.Id into joined
                    from u in joined.DefaultIfEmpty()
                    select new { Log = a, Email = u.Email };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.Log.Action.ToLower().Contains(term)
                                     || x.Log.EntityType.ToLower().Contains(term)
                                     || (x.Email != null && x.Email.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Log.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = rows.Select(x => new AuditLogDto(x.Log.Id, x.Log.UserId, x.Email, x.Log.Action, x.Log.EntityType,
            x.Log.EntityId, x.Log.SafeMetadataJson, x.Log.CreatedAtUtc)).ToList();
        return new PagedResult<AuditLogDto>(items, total, page, pageSize);
    }

    // ---------- settings + announcements ----------
    public Task<IReadOnlyList<SettingDto>> GetSettingsAsync(CancellationToken ct) => settings.GetAllAsync(ct);

    public Task UpdateSettingsAsync(Guid adminId, IReadOnlyDictionary<string, string> values, CancellationToken ct) =>
        settings.UpdateAsync(adminId, values, ct);

    public async Task<int> AnnounceAsync(Guid adminId, string title, string message, CancellationToken ct)
    {
        var count = await notifications.AnnounceAsync(title, message, ct);
        await audit.LogAsync(adminId, "admin.announcement_sent", "Notification", null, new { recipients = count }, ct);
        return count;
    }
}