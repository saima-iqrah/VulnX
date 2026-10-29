namespace VulnX.Domain.Enums;

public enum AccountStatus { Active, Suspended, Locked }

public enum ScanStatus { Queued, Running, Completed, Failed, Cancelled }

public enum ScanProfile { Quick, Standard, Deep }

public enum ModuleStatus { Pending, Running, Completed, Failed, Skipped }

public enum Severity { Informational, Low, Medium, High, Critical }

public enum Confidence { Low, Medium, High }

public enum AiMessageRole { User, Assistant, System }

public enum NotificationType { ScanCompleted, CriticalFinding, Announcement, System }
