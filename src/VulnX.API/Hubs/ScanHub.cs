using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using VulnX.API.Extensions;
using VulnX.Application.Abstractions;
using VulnX.Application.Scans;

namespace VulnX.API.Hubs;

[Authorize]
public sealed class ScanHub(IScanService scans) : Hub
{
    public static string GroupFor(Guid scanId) => "scan-" + scanId;

    /// A user can only listen to the scans they own.
    public async Task JoinScan(Guid scanId)
    {
        var userId = Context.User!.GetUserId();
        if (!await scans.OwnsAsync(userId, scanId, Context.ConnectionAborted))
            throw new HubException("Scan not found.");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(scanId));
    }

    public Task LeaveScan(Guid scanId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(scanId));
}

public sealed class SignalRScanNotifier(IHubContext<ScanHub> hub) : IScanProgressNotifier
{
    public Task PublishAsync(ScanProgressDto progress, CancellationToken ct = default) =>
        hub.Clients.Group(ScanHub.GroupFor(progress.ScanId)).SendAsync("scanProgress", progress, ct);
}