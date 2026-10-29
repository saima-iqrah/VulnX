using System.Collections.Concurrent;
using System.Threading.Channels;
using VulnX.Application.Abstractions;

namespace VulnX.Infrastructure.Scans;

internal sealed class ScanQueue : IScanQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _running = new();

    public ValueTask EnqueueAsync(Guid scanId) => _channel.Writer.WriteAsync(scanId);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);

    public CancellationTokenSource Register(Guid scanId, CancellationToken linked)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(linked);
        _running[scanId] = cts;
        return cts;
    }

    public bool RequestCancel(Guid scanId)
    {
        if (!_running.TryGetValue(scanId, out var cts)) return false;
        try { cts.Cancel(); return true; }
        catch (ObjectDisposedException) { return false; }
    }

    public void Unregister(Guid scanId)
    {
        if (_running.TryRemove(scanId, out var cts)) cts.Dispose();
    }
}