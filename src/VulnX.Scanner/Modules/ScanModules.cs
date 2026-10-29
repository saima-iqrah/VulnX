using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using VulnX.Domain.Enums;
using VulnX.Scanner.Engine;
using VulnX.Scanner.Http;

namespace VulnX.Scanner.Modules;

// All modules are passive or low-impact: they read normal responses and never send attack payloads.

public sealed class SecurityHeadersModule : IScanModule
{
    public string Name => ScanProfiles.Headers;

    public Task RunAsync(ScanContext ctx, CancellationToken ct)
    {
        var r = ctx.Root!;
        var url = r.FinalUrl.ToString();
        var isHttps = r.FinalUrl.Scheme == Uri.UriSchemeHttps;

        var csp = r.Header("Content-Security-Policy");
        if (csp is null) ctx.Add(new("HDR-001", url, "The response has no Content-Security-Policy header."));

        if (isHttps)
        {
            var hsts = r.Header("Strict-Transport-Security");
            if (hsts is null)
                ctx.Add(new("HDR-002", url, "The response has no Strict-Transport-Security header."));
            else
            {
                var m = Regex.Match(hsts, @"max-age=(\d+)", RegexOptions.IgnoreCase);
                if (!m.Success || !long.TryParse(m.Groups[1].Value, out var age) || age < 15552000)
                    ctx.Add(new("HDR-003", url, "Strict-Transport-Security: " + hsts));
            }
        }

        var framing = r.Header("X-Frame-Options") is not null ||
                      (csp?.Contains("frame-ancestors", StringComparison.OrdinalIgnoreCase) ?? false);
        if (!framing) ctx.Add(new("HDR-004", url, "Neither X-Frame-Options nor CSP frame-ancestors is present."));

        var nosniff = r.Header("X-Content-Type-Options");
        if (!string.Equals(nosniff?.Trim(), "nosniff", StringComparison.OrdinalIgnoreCase))
            ctx.Add(new("HDR-005", url, "X-Content-Type-Options: " + (nosniff ?? "(absent)")));

        if (r.Header("Referrer-Policy") is null) ctx.Add(new("HDR-006", url, "The response has no Referrer-Policy header."));
        if (r.Header("Permissions-Policy") is null) ctx.Add(new("HDR-007", url, "The response has no Permissions-Policy header."));

        return Task.CompletedTask;
    }
}

public sealed class TlsModule : IScanModule
{
    public string Name => ScanProfiles.Tls;

    public async Task RunAsync(ScanContext ctx, CancellationToken ct)
    {
        var t = ctx.Target;
        var url = t.Url.ToString();

        if (!t.IsHttps)
        {
            ctx.Add(new("TLS-001", url, "The scanned URL uses plain HTTP: " + url));
            return;
        }

        try
        {
            var info = await TlsInspector.InspectAsync(t.Hostname, t.Port, ct);
            var now = DateTime.UtcNow;
            var expired = info.NotAfter is { } end && end < now;

            if (!info.ChainValid || expired)
            {
                var extra = expired ? "; the certificate expired on " + info.NotAfter!.Value.ToString("yyyy-MM-dd") : string.Empty;
                ctx.Add(new("TLS-002", url,
                    "Certificate problem: " + info.PolicyErrors + extra + ". Subject: " + info.Subject + ". Issuer: " + info.Issuer + "."));
            }
            else if (info.NotAfter is { } soon && (soon - now).TotalDays < 30)
            {
                var days = (soon - now).TotalDays;
                ctx.Add(new("TLS-003", url,
                    "The certificate expires on " + soon.ToString("yyyy-MM-dd") + " (" + days.ToString("F0") + " days left).",
                    days < 14 ? Severity.Medium : null));
            }

            if (info.Protocol is "Tls" or "Tls11" or "Ssl3" or "Ssl2")
                ctx.Add(new("TLS-004", url, "Negotiated protocol: " + info.Protocol));
        }
        catch (Exception ex) when (ex is AuthenticationException or IOException or SocketException)
        {
            ctx.Add(new("TLS-006", url, "TLS handshake failed: " + ex.Message, null, Confidence.Low));
        }

        // Does plain HTTP redirect to HTTPS? (a single request, redirect not followed)
        if (t.Port == 443)
        {
            try
            {
                var plain = await ctx.Http.FetchAsync(new Uri("http://" + t.Hostname + "/"), followRedirects: false, ct: ct);
                var redirectsToHttps = plain.StatusCode is >= 300 and < 400 &&
                                       plain.RedirectLocation?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true;
                if (!redirectsToHttps && plain.StatusCode is >= 200 and < 300)
                    ctx.Add(new("TLS-005", plain.RequestUrl.ToString(),
                        "http://" + t.Hostname + "/ returned HTTP " + plain.StatusCode + " without redirecting to HTTPS."));
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !ct.IsCancellationRequested))
            {
                // port 80 closed or slow: nothing to report
            }
        }
    }
}

public sealed class CookieModule : IScanModule
{
    private static readonly Regex SessionLike = new("sess|sid|auth|token|jwt|login", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public string Name => ScanProfiles.Cookies;

    public Task RunAsync(ScanContext ctx, CancellationToken ct)
    {
        var r = ctx.Root!;
        var url = r.FinalUrl.ToString();
        var isHttps = r.FinalUrl.Scheme == Uri.UriSchemeHttps;

        foreach (var raw in r.SetCookies)
        {
            var parts = raw.Split(';').Select(p => p.Trim()).ToArray();
            var name = parts[0].Split('=')[0];                    // the cookie VALUE is never stored
            var attrs = parts.Skip(1).ToList();
            bool Has(string a) => attrs.Any(x => x.Equals(a, StringComparison.OrdinalIgnoreCase) || x.StartsWith(a + "=", StringComparison.OrdinalIgnoreCase));
            var sensitive = SessionLike.IsMatch(name);

            if (isHttps && !Has("Secure")) ctx.Add(new("CKE-001", url, "Cookie '" + name + "' is set without the Secure attribute."));
            if (!Has("HttpOnly"))
                ctx.Add(new("CKE-002", url, "Cookie '" + name + "' is set without the HttpOnly attribute.", sensitive ? Severity.Medium : null));
            if (!Has("SameSite")) ctx.Add(new("CKE-003", url, "Cookie '" + name + "' is set without a SameSite attribute."));
        }
        return Task.CompletedTask;
    }
}

public sealed class CorsModule : IScanModule
{
    private const string ProbeOrigin = "https://vulnx-cors-probe.example";
    public string Name => ScanProfiles.Cors;

    public async Task RunAsync(ScanContext ctx, CancellationToken ct)
    {
        var url = ctx.Root!.FinalUrl;
        var r = await ctx.Http.FetchAsync(url, origin: ProbeOrigin, ct: ct);
        var allowOrigin = r.Header("Access-Control-Allow-Origin");
        var credentials = string.Equals(r.Header("Access-Control-Allow-Credentials"), "true", StringComparison.OrdinalIgnoreCase);

        if (allowOrigin == ProbeOrigin)
            ctx.Add(new(credentials ? "COR-002" : "COR-001", url.ToString(),
                "The probe origin " + ProbeOrigin + " was reflected in Access-Control-Allow-Origin" +
                (credentials ? " together with Access-Control-Allow-Credentials: true." : ".")));
        else if (allowOrigin == "*")
            ctx.Add(new("COR-003", url.ToString(), "Access-Control-Allow-Origin: *"));
    }
}

public sealed class InfoDisclosureModule : IScanModule
{
    public string Name => ScanProfiles.Info;

    public async Task RunAsync(ScanContext ctx, CancellationToken ct)
    {
        var r = ctx.Root!;
        var url = r.FinalUrl.ToString();

        var server = r.Header("Server");
        if (server is not null && Regex.IsMatch(server, @"\d+\.\d+"))
            ctx.Add(new("INF-001", url, "Server: " + server));

        foreach (var header in new[] { "X-Powered-By", "X-AspNet-Version", "X-AspNetMvc-Version", "X-Generator" })
        {
            var value = r.Header(header);
            if (value is not null) ctx.Add(new("INF-002", url, header + ": " + value));
        }

        var root = new Uri(ctx.Target.Url, "/");

        async Task<HttpSnapshot?> TryGet(string path)
        {
            try { return await ctx.Http.FetchAsync(new Uri(root, path), followRedirects: false, ct: ct); }
            catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !ct.IsCancellationRequested))
            {
                return null;
            }
        }

        try
        {
            var git = await TryGet(".git/HEAD");
            if (git is { StatusCode: 200 } && git.Body.TrimStart().StartsWith("ref:", StringComparison.Ordinal))
                ctx.Add(new("INF-003", new Uri(root, ".git/HEAD").ToString(), "GET /.git/HEAD returned HTTP 200 with a Git reference file."));

            var env = await TryGet(".env");
            if (env is { StatusCode: 200 } &&
                !env.ContentType.Contains("html", StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(env.Body, @"^[A-Z][A-Z0-9_]{2,}\s*=", RegexOptions.Multiline, TimeSpan.FromSeconds(2)))
                ctx.Add(new("INF-004", new Uri(root, ".env").ToString(), "GET /.env returned HTTP 200 with environment-variable style content (content not stored)."));
        }
        catch (RegexMatchTimeoutException)
        {
        }
    }
}

public sealed class HtmlContentModule : IScanModule
{
    private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(2);
    public string Name => ScanProfiles.Html;

    public Task RunAsync(ScanContext ctx, CancellationToken ct)
    {
        var r = ctx.Root!;
        if (!r.ContentType.Contains("html", StringComparison.OrdinalIgnoreCase)) return Task.CompletedTask;

        var url = r.FinalUrl.ToString();
        var isHttps = r.FinalUrl.Scheme == Uri.UriSchemeHttps;

        try
        {
            if (isHttps)
            {
                var mixed = Regex.Matches(r.Body, @"<(?:script|iframe|link)\b[^>]*?(?:src|href)\s*=\s*[""'](http://[^""']+)[""']", Opts, Limit)
                    .Select(m => m.Groups[1].Value).Distinct().Take(3).ToList();
                if (mixed.Count > 0)
                    ctx.Add(new("HTM-001", url, "Resources loaded over HTTP: " + string.Join(", ", mixed)));

                var forms = Regex.Matches(r.Body, @"<form\b[^>]*?action\s*=\s*[""'](http://[^""']+)[""']", Opts, Limit)
                    .Select(m => m.Groups[1].Value).Distinct().Take(3).ToList();
                if (forms.Count > 0)
                    ctx.Add(new("HTM-003", url, "Form action over HTTP: " + string.Join(", ", forms)));
            }

            var withoutSri = new List<string>();
            foreach (Match m in Regex.Matches(r.Body, @"<script\b[^>]*?\bsrc\s*=\s*[""'](https?://[^""']+)[""'][^>]*>", Opts, Limit))
            {
                if (m.Value.Contains("integrity=", StringComparison.OrdinalIgnoreCase)) continue;
                if (Uri.TryCreate(m.Groups[1].Value, UriKind.Absolute, out var u) && !ctx.Http.IsInScope(u))
                    withoutSri.Add(m.Groups[1].Value);
            }
            if (withoutSri.Count > 0)
                ctx.Add(new("HTM-002", url, "External scripts without integrity attribute: " + string.Join(", ", withoutSri.Distinct().Take(3))));
        }
        catch (RegexMatchTimeoutException)
        {
        }

        return Task.CompletedTask;
    }
}