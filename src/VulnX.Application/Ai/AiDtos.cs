using System.ComponentModel.DataAnnotations;

namespace VulnX.Application.Ai;

public sealed class AiChatRequest
{
    [Required, StringLength(1000, MinimumLength = 1)] public string Message { get; set; } = string.Empty;
    public Guid? ResultId { get; set; }
}

public sealed class AiAskTurn
{
    public string Role { get; set; } = "user";
    [StringLength(4000)] public string Content { get; set; } = string.Empty;
}

/// General assistant request: the browser keeps the conversation and sends the recent turns with each question.
public sealed class AiAskRequest
{
    [Required, StringLength(1000, MinimumLength = 1)] public string Message { get; set; } = string.Empty;
    public Guid? ScanId { get; set; }
    public List<AiAskTurn>? History { get; set; }
}

public sealed record AiExplanationDto(
    Guid ResultId, string Summary, string WhyItMatters, IReadOnlyList<string> FixSteps,
    string Caveats, string Source, string Disclaimer);

public sealed record AiScanSummaryDto(string Summary, IReadOnlyList<string> Priorities, string Source, string Disclaimer);

public sealed record AiChatReplyDto(Guid ConversationId, string Reply, string Source, string Disclaimer);

public sealed record AiAskReplyDto(string Reply, string Source, string ContextLabel, IReadOnlyList<string> Suggestions, string Disclaimer);

public sealed record AiMessageDto(string Role, string Content, DateTime CreatedAtUtc);