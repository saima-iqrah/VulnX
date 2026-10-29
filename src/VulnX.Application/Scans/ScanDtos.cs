using System.ComponentModel.DataAnnotations;

namespace VulnX.Application.Scans;

public sealed class CreateScanRequest
{
    [Required, StringLength(2048, MinimumLength = 4)] public string Url { get; set; } = string.Empty;
    [Required] public string Profile { get; set; } = "Quick";
    public bool AuthorizationConfirmed { get; set; }
}

public sealed class UpdateScanRequest
{
    [StringLength(120)] public string? Label { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    public List<string>? Tags { get; set; }
}

public sealed record ScanModuleDto(string Name, string Status, int ProgressPercent, string? Error);

public sealed record ScanResultDto(
    Guid Id, string Code, string Title, string Category, string Severity, string Confidence,
    string Description, string RootCause, string Impact, string AffectedUrl, string Evidence,
    string Recommendation, IReadOnlyList<string> References, string DetectionModule, DateTime DetectedAtUtc);

public sealed record ScanSummaryDto(
    Guid Id, string Target, string Profile, string Status, int ProgressPercent, int? Score, string? Grade,
    int FindingsCount, string? Label, IReadOnlyList<string> Tags, DateTime CreatedAtUtc, DateTime? CompletedAtUtc);

public sealed record ScanDetailDto(
    Guid Id, string Target, string Profile, string Status, int ProgressPercent, int? Score, string? Grade,
    string? Error, string? Label, string? Notes, IReadOnlyList<string> Tags,
    DateTime CreatedAtUtc, DateTime? StartedAtUtc, DateTime? CompletedAtUtc,
    IReadOnlyList<ScanModuleDto> Modules, IReadOnlyList<ScanResultDto> Results);

public sealed record ScanProgressDto(
    Guid ScanId, string Status, int ProgressPercent, string? CurrentModule,
    IReadOnlyList<ScanModuleDto> Modules, int FindingsCount, int? Score, string? Grade, string? Error);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record FindingKeyDto(string Code, string Title, string Severity, string AffectedUrl, string Evidence);

public sealed record ScanCompareDto(
    ScanSummaryDto Base, ScanSummaryDto Target, int? ScoreDelta,
    IReadOnlyList<FindingKeyDto> Added, IReadOnlyList<FindingKeyDto> Resolved, IReadOnlyList<FindingKeyDto> Unchanged);

public sealed record TrendPointDto(Guid ScanId, DateTime Date, int Score, int Critical, int High, int Medium, int Low);

public sealed record DashboardDto(
    int TotalScans, int CompletedScans, int? LatestScore, string? LatestGrade,
    int OpenFindings, int CriticalFindings, IReadOnlyDictionary<string, int> SeverityCounts,
    IReadOnlyList<TrendPointDto> Trend, IReadOnlyList<ScanSummaryDto> Recent);