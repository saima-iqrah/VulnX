using System.Diagnostics;

namespace VulnX.API.Middleware;

/// Adds browser security headers to every response.
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext ctx)
    {
        ctx.Response.OnStarting(() =>
        {
            var h = ctx.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            if (ctx.Request.Path.StartsWithSegments("/api") || ctx.Request.Path.StartsWithSegments("/hubs"))
            {
                h["Cache-Control"] = "no-store";
                h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            }
            else
            {
                h["Content-Security-Policy"] =
                    "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; " +
                    "connect-src 'self' ws: wss:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
            }
            return Task.CompletedTask;
        });
        return next(ctx);
    }
}

/// One structured log line per API request (method, path, status, duration, user id). Query strings are never logged.
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var sw = Stopwatch.StartNew();
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = ctx.TraceIdentifier });
        try
        {
            await next(ctx);
        }
        finally
        {
            sw.Stop();
            var path = ctx.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms (user {UserId})",
                    ctx.Request.Method, path, ctx.Response.StatusCode, sw.ElapsedMilliseconds,
                    ctx.User.FindFirst("sub")?.Value ?? "anonymous");
            }
        }
    }
}