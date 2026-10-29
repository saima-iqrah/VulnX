using VulnX.Application.Ai;

namespace VulnX.Application.Abstractions;

/// Explains STORED scan findings and answers questions. It never creates findings and never changes scores or severities.
public interface IAiAssistant
{
    bool IsAiConfigured { get; }
    Task<AiExplanationDto> ExplainFindingAsync(Guid userId, Guid scanId, Guid resultId, CancellationToken ct);
    Task<AiScanSummaryDto> SummarizeScanAsync(Guid userId, Guid scanId, CancellationToken ct);
    Task<AiChatReplyDto> ChatAsync(Guid userId, Guid scanId, AiChatRequest request, CancellationToken ct);
    Task<AiAskReplyDto> AskAsync(Guid userId, AiAskRequest request, CancellationToken ct);
    Task<IReadOnlyList<AiMessageDto>> GetConversationAsync(Guid userId, Guid scanId, CancellationToken ct);
    Task ClearConversationAsync(Guid userId, Guid scanId, CancellationToken ct);
}