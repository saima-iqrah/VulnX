using VulnX.Domain.Common;
using VulnX.Domain.Enums;

namespace VulnX.Domain.Entities;

// NOTE: UserId is a plain Guid. The Identity user lives in Infrastructure so Domain stays framework-free.

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;   // store hash only, never the raw token
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}

public class ScanTarget : BaseEntity
{
    public Guid UserId { get; set; }
    public string NormalizedUrl { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string ScopePolicy { get; set; } = "ExactHost";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<Scan> Scans { get; set; } = new();
}

public class Scan : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ScanTargetId { get; set; }
    public ScanTarget Target { get; set; } = null!;
    public ScanProfile Profile { get; set; } = ScanProfile.Quick;
    public ScanStatus Status { get; set; } = ScanStatus.Queued;
    public DateTime AuthorizationConfirmedAtUtc { get; set; }   // explicit authorization is mandatory
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int ProgressPercent { get; set; }
    public int? Score { get; set; }                              // deterministic 0-100, never set by AI
    public string? Error { get; set; }
    public List<ScanModule> Modules { get; set; } = new();
    public string? Label { get; set; }
    public string? Notes { get; set; }
    public List<Tag> Tags { get; set; } = new();
    public List<ScanResult> Results { get; set; } = new();
}

public class ScanModule : BaseEntity
{
    public Guid ScanId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public ModuleStatus Status { get; set; } = ModuleStatus.Pending;
    public int ProgressPercent { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Error { get; set; }
}

public class VulnerabilityDefinition : BaseEntity
{
    public string Code { get; set; } = string.Empty;             // unique, e.g. HDR-001
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Severity DefaultSeverity { get; set; } = Severity.Informational;
    public List<string> References { get; set; } = new();
}

public class ScanResult : BaseEntity
{
    public Guid ScanId { get; set; }
    public Guid VulnerabilityDefinitionId { get; set; }
    public VulnerabilityDefinition Definition { get; set; } = null!;
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Severity Severity { get; set; }
    public Confidence Confidence { get; set; }
    public decimal? RiskScore { get; set; }                      // CVSS-compatible where applicable
    public string Description { get; set; } = string.Empty;
    public string RootCause { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string AffectedUrl { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;        // scanner evidence = source of truth
    public string? ResponseMetadataJson { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public List<string> References { get; set; } = new();
    public string DetectionModule { get; set; } = string.Empty;
    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Report : BaseEntity
{
    public Guid ScanId { get; set; }
    public string ReportNumber { get; set; } = string.Empty;     // unique
    public string? MetadataJson { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string VerificationHash { get; set; } = string.Empty;
}

public class AiConversation : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ScanId { get; set; }
    public Guid? ScanResultId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<AiMessage> Messages { get; set; } = new();
}

public class AiMessage : BaseEntity
{
    public Guid ConversationId { get; set; }
    public AiMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
}

public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? SafeMetadataJson { get; set; }                // never store secrets/PII here
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class SystemSetting
{
    public string Key { get; set; } = string.Empty;              // primary key
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedByUserId { get; set; }
}


public class Tag : BaseEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Scan> Scans { get; set; } = new();
}
