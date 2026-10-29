using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VulnX.API.Extensions;
using VulnX.Application.Abstractions;
using VulnX.Application.Platform;
using VulnX.Application.Scans;

namespace VulnX.API.Controllers;

[ApiController]
public sealed class ReportsController(IReportService reports) : ControllerBase
{
    [Authorize]
    [HttpPost("~/api/scans/{scanId:guid}/report")]
    public async Task<ActionResult<ReportInfoDto>> Create(Guid scanId, CancellationToken ct)
        => Ok(await reports.GetOrCreateAsync(User.GetUserId(), scanId, ct));

    [Authorize]
    [HttpGet("~/api/scans/{scanId:guid}/report/pdf")]
    public async Task<IActionResult> Pdf(Guid scanId, CancellationToken ct)
    {
        var (content, fileName) = await reports.BuildPdfAsync(User.GetUserId(), scanId, ct);
        return File(content, "application/pdf", fileName);
    }

    /// Public: anyone holding a report number and verification code can check that it is genuine.
    [AllowAnonymous]
    [EnableRateLimiting("verify")]
    [HttpGet("~/api/reports/verify")]
    public async Task<ActionResult<VerifyReportResultDto>> Verify([FromQuery] string number, [FromQuery] string code, CancellationToken ct)
        => Ok(await reports.VerifyAsync(number, code, ct));
}

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationDto>>> List(int page = 1, int pageSize = 20, bool unreadOnly = false, CancellationToken ct = default)
        => Ok(await notifications.ListAsync(User.GetUserId(), page, pageSize, unreadOnly, ct));

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> UnreadCount(CancellationToken ct)
        => Ok(new UnreadCountDto(await notifications.UnreadCountAsync(User.GetUserId(), ct)));

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(User.GetUserId(), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await notifications.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}