using VulnX.Application.Platform;
using VulnX.Application.Scans;

namespace VulnX.Application.Abstractions;

public interface ISystemSettings
{
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default);
    Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default);
    Task<IReadOnlyList<SettingDto>> GetAllAsync(CancellationToken ct = default);
    Task UpdateAsync(Guid adminId, IReadOnlyDictionary<string, string> values, CancellationToken ct = default);
}

public interface INotificationService
{
    Task NotifyScanFinishedAsync(Guid scanId, CancellationToken ct = default);
    Task<PagedResult<NotificationDto>> ListAsync(Guid userId, int page, int pageSize, bool unreadOnly, CancellationToken ct);
    Task<int> UnreadCountAsync(Guid userId, CancellationToken ct);
    Task MarkReadAsync(Guid userId, Guid id, CancellationToken ct);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct);
    Task<int> AnnounceAsync(string title, string message, CancellationToken ct);
}

public interface IReportService
{
    Task<ReportInfoDto> GetOrCreateAsync(Guid userId, Guid scanId, CancellationToken ct);
    Task<(byte[] Content, string FileName)> BuildPdfAsync(Guid userId, Guid scanId, CancellationToken ct);
    Task<VerifyReportResultDto> VerifyAsync(string number, string code, CancellationToken ct);
}

public interface IAdminService
{
    Task<AdminOverviewDto> OverviewAsync(CancellationToken ct);
    Task<SystemHealthDto> HealthAsync(CancellationToken ct);
    Task<PagedResult<AdminUserDto>> ListUsersAsync(string? search, string? status, string? role, int page, int pageSize, CancellationToken ct);
    Task SetStatusAsync(Guid adminId, Guid userId, string status, CancellationToken ct);
    Task SetRoleAsync(Guid adminId, Guid userId, bool makeAdmin, CancellationToken ct);
    Task DeleteUserAsync(Guid adminId, Guid userId, CancellationToken ct);
    Task<PagedResult<AdminScanDto>> ListScansAsync(string? search, string? status, int page, int pageSize, CancellationToken ct);
    Task DeleteScanAsync(Guid adminId, Guid scanId, CancellationToken ct);
    Task<PagedResult<AuditLogDto>> ListAuditAsync(string? search, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<SettingDto>> GetSettingsAsync(CancellationToken ct);
    Task UpdateSettingsAsync(Guid adminId, IReadOnlyDictionary<string, string> values, CancellationToken ct);
    Task<int> AnnounceAsync(Guid adminId, string title, string message, CancellationToken ct);
}