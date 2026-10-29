using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VulnX.API.Extensions;
using VulnX.Application.Abstractions;
using VulnX.Application.Ai;

namespace VulnX.API.Controllers;

[ApiController]
[Authorize]
public sealed class AiController(IAiAssistant ai) : ControllerBase
{
    [HttpGet("~/api/ai/status")]
    public IActionResult Status() => Ok(new { configured = ai.IsAiConfigured });

    /// General assistant used by the floating button (works with or without a scan).
    [HttpPost("~/api/ai/ask")]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<AiAskReplyDto>> Ask(AiAskRequest request, CancellationToken ct)
        => Ok(await ai.AskAsync(User.GetUserId(), request, ct));

    [HttpPost("~/api/scans/{scanId:guid}/findings/{resultId:guid}/explain")]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<AiExplanationDto>> Explain(Guid scanId, Guid resultId, CancellationToken ct)
        => Ok(await ai.ExplainFindingAsync(User.GetUserId(), scanId, resultId, ct));

    [HttpPost("~/api/scans/{scanId:guid}/ai/summary")]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<AiScanSummaryDto>> Summary(Guid scanId, CancellationToken ct)
        => Ok(await ai.SummarizeScanAsync(User.GetUserId(), scanId, ct));

    [HttpPost("~/api/scans/{scanId:guid}/ai/chat")]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<AiChatReplyDto>> Chat(Guid scanId, AiChatRequest request, CancellationToken ct)
        => Ok(await ai.ChatAsync(User.GetUserId(), scanId, request, ct));

    [HttpGet("~/api/scans/{scanId:guid}/ai/conversation")]
    public async Task<ActionResult<IReadOnlyList<AiMessageDto>>> Conversation(Guid scanId, CancellationToken ct)
        => Ok(await ai.GetConversationAsync(User.GetUserId(), scanId, ct));

    [HttpDelete("~/api/scans/{scanId:guid}/ai/conversation")]
    public async Task<IActionResult> ClearConversation(Guid scanId, CancellationToken ct)
    {
        await ai.ClearConversationAsync(User.GetUserId(), scanId, ct);
        return NoContent();
    }
}