using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VulnX.API.Extensions;
using VulnX.Application.Abstractions;
using VulnX.Application.Platform;
using VulnX.Application.Scans;

namespace VulnX.API.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin")]
public sealed class AdminController(IAdminService admin) : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { message = "admin ok" });

    [HttpGet("overview")]
    public async Task<ActionResult<AdminOverviewDto>> Overview(CancellationToken ct) => Ok(await admin.OverviewAsync(ct));

    [HttpGet("health")]
    public async Task<ActionResult<SystemHealthDto>> Health(CancellationToken ct) => Ok(await admin.HealthAsync(ct));

    // ---- users ----
    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> Users(string? search = null, string? status = null, string? role = null,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
        => Ok(await admin.ListUsersAsync(search, status, role, page, pageSize, ct));

    [HttpPost("users/{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, SetStatusRequest request, CancellationToken ct)
    {
        await admin.SetStatusAsync(User.GetUserId(), id, request.Status, ct);
        return NoContent();
    }

    [HttpPost("users/{id:guid}/role")]
    public async Task<IActionResult> SetRole(Guid id, SetRoleRequest request, CancellationToken ct)
    {
        await admin.SetRoleAsync(User.GetUserId(), id, request.Admin, ct);
        return NoContent();
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        await admin.DeleteUserAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    // ---- scans ----
    [HttpGet("scans")]
    public async Task<ActionResult<PagedResult<AdminScanDto>>> Scans(string? search = null, string? status = null,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
        => Ok(await admin.ListScansAsync(search, status, page, pageSize, ct));

    [HttpDelete("scans/{id:guid}")]
    public async Task<IActionResult> DeleteScan(Guid id, CancellationToken ct)
    {
        await admin.DeleteScanAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    // ---- audit ----
    [HttpGet("audit")]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> Audit(string? search = null, int page = 1, int pageSize = 30, CancellationToken ct = default)
        => Ok(await admin.ListAuditAsync(search, page, pageSize, ct));

    // ---- settings + announcements ----
    [HttpGet("settings")]
    public async Task<ActionResult<IReadOnlyList<SettingDto>>> Settings(CancellationToken ct) => Ok(await admin.GetSettingsAsync(ct));

    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings(Dictionary<string, string> values, CancellationToken ct)
    {
        await admin.UpdateSettingsAsync(User.GetUserId(), values, ct);
        return NoContent();
    }

    [HttpPost("announcements")]
    public async Task<IActionResult> Announce(AnnouncementRequest request, CancellationToken ct)
    {
        var sent = await admin.AnnounceAsync(User.GetUserId(), request.Title, request.Message, ct);
        return Ok(new { sent });
    }
}