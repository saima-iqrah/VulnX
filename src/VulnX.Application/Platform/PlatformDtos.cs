using System.ComponentModel.DataAnnotations;

namespace VulnX.Application.Platform;

// ---------- reports ----------
public sealed record ReportInfoDto(Guid ScanId, string ReportNumber, string VerificationCode, DateTime GeneratedAtUtc, string VerifyUrl);

public sealed record VerifyReportResultDto(
    bool Valid, string Message, string? ReportNumber, string? Host, DateTime? GeneratedAtUtc,
    int? Score, string? Grade, int? Findings, int? Critical, int? High);

// ---------- notifications ----------
public sealed record NotificationDto(Guid Id, string Type, string Title, string Message, DateTime CreatedAtUtc, bool IsRead);
public sealed record UnreadCountDto(int Count);

// ---------- admin ----------
public sealed class SetStatusRequest
{
    [Required] public string Status { get; set; } = string.Empty;
}

public sealed class SetRoleRequest
{
    public bool Admin { get; set; }
}

public sealed class AnnouncementRequest
{
    [Required, StringLength(120, MinimumLength = 2)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(1000, MinimumLength = 2)] public string Message { get; set; } = string.Empty;
}

public sealed record AdminUserDto(
    Guid Id, string Email, string FullName, string Status, bool IsAdmin, bool EmailConfirmed,
    int ScanCount, DateTime CreatedAtUtc, DateTime? LastLoginAtUtc);

public sealed record AdminScanDto(
    Guid Id, string Target, string OwnerEmail, string Profile, string Status, int? Score, string? Grade,
    int FindingsCount, string? Label, DateTime CreatedAtUtc);

public sealed record AuditLogDto(
    Guid Id, Guid? UserId, string? UserEmail, string Action, string EntityType, string? EntityId,
    string? Metadata, DateTime CreatedAtUtc);

public sealed record SettingDto(string Key, string Value, string DefaultValue, string Description);

public sealed record DailyCountDto(DateTime Date, int Count);

public sealed record AdminOverviewDto(
    int Users, int SuspendedUsers, int Admins, int NewUsers7d, int Scans, int Scans24h, int RunningScans,
    int FailedScans24h, int CriticalFindings, int AuditEvents24h, IReadOnlyList<DailyCountDto> ScansPerDay);

public sealed record SystemHealthDto(
    string Status, bool DatabaseOk, long DatabaseLatencyMs, int QueuedScans, int RunningScans,
    DateTime? LastCompletedScanUtc, bool AiConfigured, bool EmailConfigured, string Environment,
    string Version, long UptimeSeconds, long MemoryMb, DateTime ServerTimeUtc);

public static class SettingDefinitions
{
    public sealed record Def(string Default, string Description);

    public static readonly IReadOnlyDictionary<string, Def> All = new Dictionary<string, Def>
    {
        ["registration.enabled"] = new("true", "Allow new users to register (true or false)."),
        ["scan.maxActive"] = new("2", "Maximum scans one user can run at the same time (1-10)."),
        ["scan.maxPerHour"] = new("10", "Maximum scans one user can start per hour (1-100).")
    };

    /// Returns an error message, or null when the value is valid.
    public static string? Validate(string key, string value)
    {
        if (!All.ContainsKey(key)) return $"Unknown setting '{key}'.";
        switch (key)
        {
            case "registration.enabled":
                return bool.TryParse(value, out _) ? null : "registration.enabled must be true or false.";
            case "scan.maxActive":
                return int.TryParse(value, out var active) && active is >= 1 and <= 10 ? null : "scan.maxActive must be a number from 1 to 10.";
            case "scan.maxPerHour":
                return int.TryParse(value, out var hourly) && hourly is >= 1 and <= 100 ? null : "scan.maxPerHour must be a number from 1 to 100.";
            default:
                return null;
        }
    }
}