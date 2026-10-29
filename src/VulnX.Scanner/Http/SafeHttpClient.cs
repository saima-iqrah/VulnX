using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using VulnX.Scanner.Security;

namespace VulnX.Scanner.Http;

public sealed class ScanBudgetExceededException() : Exception("The request budget for this scan was exhausted.");

public sealed record HttpSnapshot(
    Uri RequestUrl, Uri FinalUrl, int StatusCode,
    IReadOnlyDictionary<string, string[]> Headers, IReadOnlyList<string> SetCookies,
    string ContentType, string Body, string? RedirectLocation)
{
    public string? Header(string name) => Headers.TryGetValue(name, out var v) ? string.Join(", ", v) : null;
}

/// HTTP client for scanning. Protections: connects only to validated public IPs (also on every redirect and after
/// every DNS lookup, which defeats DNS rebinding), never uses a proxy, never leaves the target host (plus its
/// www variant), follows at most 3 redirects, caps the response size, enforces a request budget and a delay.
public sealed class SafeHttpClient : IDisposable
{
    private const int MaxBodyBytes = 512 * 1024;
    private readonly HttpClient _client;
    private readonly HashSet<string> _hosts;
    private readonly int _maxRequests;
    private readonly TimeSpan _delay;
    private int _used;
    private DateTime _last = DateTime.MinValue;

    public SafeHttpClient(string allowedHost, int maxRequests, TimeSpan requestTimeout, TimeSpan delay)
    {
        _maxRequests = maxRequests;
        _delay = delay;
        _hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            allowedHost,
            allowedHost.StartsWith("www.", StringComparison.Ordinal) ? allowedHost[4..] : "www." + allowedHost
        };

        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            ConnectTimeout = TimeSpan.FromSeconds(8),
            PooledConnectionLifetime = TimeSpan.FromMinutes(1),
            ConnectCallback = GuardedConnectAsync,
            // Certificates are accepted on purpose so sites with broken certificates can still be assessed.
            // The TLS module validates and reports certificate problems separately.
            SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true }
        };

        _client = new HttpClient(handler, disposeHandler: true) { Timeout = requestTimeout };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("VulnX-Scanner/1.0 (authorized security assessment)");
        _client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,*/*;q=0.8");
    }

    public bool IsInScope(Uri url) =>
        (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps) && _hosts.Contains(url.IdnHost.Trim('[', ']'));

    public async Task<HttpSnapshot> FetchAsync(Uri url, string? origin = null, bool followRedirects = true, CancellationToken ct = default)
    {
        var current = url;
        for (var hop = 0; ; hop++)
        {
            if (!IsInScope(current))
                throw new SsrfBlockedException("Request blocked: the address is outside the scan scope.");

            await ThrottleAsync(ct);

            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            if (origin is not null) request.Headers.TryAddWithoutValidation("Origin", origin);

            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var status = (int)response.StatusCode;

            Uri? next = null;
            if (response.Headers.Location is { } location)
                next = location.IsAbsoluteUri ? location : new Uri(current, location);

            if (followRedirects && status is >= 300 and < 400 && next is not null && hop < 3 && IsInScope(next))
            {
                current = next;
                continue;
            }

            var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in response.Headers) headers[h.Key] = h.Value.ToArray();
            foreach (var h in response.Content.Headers) headers[h.Key] = h.Value.ToArray();
            var cookies = headers.TryGetValue("Set-Cookie", out var sc) ? sc.ToList() : new List<string>();
            var body = await ReadBodyAsync(response, ct);

            return new HttpSnapshot(url, current, status, headers, cookies,
                response.Content.Headers.ContentType?.MediaType ?? string.Empty, body, next?.ToString());
        }
    }

    private async Task ThrottleAsync(CancellationToken ct)
    {
        if (_used >= _maxRequests) throw new ScanBudgetExceededException();
        var wait = _last + _delay - DateTime.UtcNow;
        if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
        _used++;
        _last = DateTime.UtcNow;
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[16 * 1024];
        using var ms = new MemoryStream();
        int read;
        while (ms.Length < MaxBodyBytes && (read = await stream.ReadAsync(buffer.AsMemory(), ct)) > 0)
            ms.Write(buffer, 0, read);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static async ValueTask<Stream> GuardedConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? new[] { literal }
            : await Dns.GetHostAddressesAsync(host, ct);

        if (addresses.Length == 0 || addresses.Any(a => !IpGuard.IsPublic(a)))
            throw new SsrfBlockedException($"Connection to {host} blocked: it resolves to a non-public address.");

        Exception? last = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex)
            {
                socket.Dispose();
                last = ex;
            }
        }
        throw last ?? new SocketException();
    }

    public void Dispose() => _client.Dispose();
}

public sealed record TlsInfo(string Protocol, DateTime? NotAfter, string Subject, string Issuer, bool ChainValid, string PolicyErrors);

/// Reads TLS protocol and certificate details (passive, a normal handshake only) from a validated public IP.
public static class TlsInspector
{
    public static async Task<TlsInfo> InspectAsync(string host, int port, CancellationToken ct)
    {
        var addresses = IPAddress.TryParse(host, out var literal)
            ? new[] { literal }
            : await Dns.GetHostAddressesAsync(host, ct);
        if (addresses.Length == 0 || addresses.Any(a => !IpGuard.IsPublic(a)))
            throw new SsrfBlockedException("TLS inspection blocked: the host resolves to a non-public address.");

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(TimeSpan.FromSeconds(10));

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(addresses[0], port, limit.Token);

        var errors = SslPolicyErrors.None;
        X509Certificate2? cert = null;
        await using var ssl = new SslStream(tcp.GetStream(), false, (_, c, _, e) =>
        {
            errors = e;
            if (c is not null) cert = new X509Certificate2(c);
            return true;   // accept so we can still read and report the problem
        });

        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host }, limit.Token);

        var info = new TlsInfo(
            ssl.SslProtocol.ToString(),
            cert?.NotAfter.ToUniversalTime(),
            cert?.Subject ?? string.Empty,
            cert?.Issuer ?? string.Empty,
            errors == SslPolicyErrors.None,
            errors.ToString());
        cert?.Dispose();
        return info;
    }
}