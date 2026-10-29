using VulnX.Application.Scans;

namespace VulnX.Application.Abstractions;

public interface IScanService
{
    Task<ScanSummaryDto> CreateAsync(Guid userId, CreateScanRequest request, CancellationToken ct);
    Task<PagedResult<ScanSummaryDto>> ListAsync(Guid userId, int page, int pageSize, string? status, string? search, string? tag, CancellationToken ct);
    Task<ScanDetailDto> GetAsync(Guid userId, Guid scanId, CancellationToken ct);
    Task<ScanDetailDto> UpdateAsync(Guid userId, Guid scanId, UpdateScanRequest request, CancellationToken ct);
    Task DeleteAsync(Guid userId, Guid scanId, CancellationToken ct);
    Task CancelAsync(Guid userId, Guid scanId, CancellationToken ct);
    Task<ScanCompareDto> CompareAsync(Guid userId, Guid baseId, Guid targetId, CancellationToken ct);
    Task<DashboardDto> DashboardAsync(Guid userId, CancellationToken ct);
    Task<bool> OwnsAsync(Guid userId, Guid scanId, CancellationToken ct);
}

public interface IScanQueue
{
    ValueTask EnqueueAsync(Guid scanId);
    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken ct);
    CancellationTokenSource Register(Guid scanId, CancellationToken linked);
    bool RequestCancel(Guid scanId);
    void Unregister(Guid scanId);
}

public interface IScanProgressNotifier
{
    Task PublishAsync(ScanProgressDto progress, CancellationToken ct = default);
}

public sealed record ValidatedTarget(Uri Url, string Hostname, int Port, bool IsHttps);

public interface ITargetValidator
{
    /// Throws AppException(400) when the target is invalid, internal, or not allowed.
    Task<ValidatedTarget> ValidateAsync(string input, CancellationToken ct);
}