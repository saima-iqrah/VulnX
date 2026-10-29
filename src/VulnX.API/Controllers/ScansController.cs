using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VulnX.API.Extensions;
using VulnX.Application.Abstractions;
using VulnX.Application.Scans;

namespace VulnX.API.Controllers;

[ApiController]
[Authorize]
[Route("api/scans")]
public sealed class ScansController(IScanService scans) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("scans")]
    public async Task<ActionResult<ScanSummaryDto>> Create(CreateScanRequest request, CancellationToken ct)
    {
        var scan = await scans.CreateAsync(User.GetUserId(), request, ct);
        return Accepted("/api/scans/" + scan.Id, scan);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ScanSummaryDto>>> List(
        int page = 1, int pageSize = 20, string? status = null, string? search = null, string? tag = null,
        CancellationToken ct = default)
        => Ok(await scans.ListAsync(User.GetUserId(), page, pageSize, status, search, tag, ct));

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> Dashboard(CancellationToken ct)
        => Ok(await scans.DashboardAsync(User.GetUserId(), ct));

    [HttpGet("compare")]
    public async Task<ActionResult<ScanCompareDto>> Compare(Guid baseId, Guid targetId, CancellationToken ct)
        => Ok(await scans.CompareAsync(User.GetUserId(), baseId, targetId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ScanDetailDto>> Get(Guid id, CancellationToken ct)
        => Ok(await scans.GetAsync(User.GetUserId(), id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ScanDetailDto>> Update(Guid id, UpdateScanRequest request, CancellationToken ct)
        => Ok(await scans.UpdateAsync(User.GetUserId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await scans.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await scans.CancelAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}