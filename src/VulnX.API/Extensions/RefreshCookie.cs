namespace VulnX.API.Extensions;

/// Refresh token lives in an HttpOnly, SameSite=Strict cookie scoped to /api/auth,
/// so JavaScript can never read it and it is only sent to the auth endpoints.
public static class RefreshCookie
{
    public const string Name = "vulnx_rt";
    private const string CookiePath = "/api/auth";

    public static void Set(HttpContext ctx, string token, DateTime expiresUtc, bool isDevelopment) =>
        ctx.Response.Cookies.Append(Name, token, Options(ctx, isDevelopment, expiresUtc));

    public static void Clear(HttpContext ctx, bool isDevelopment) =>
        ctx.Response.Cookies.Delete(Name, Options(ctx, isDevelopment, null));

    private static CookieOptions Options(HttpContext ctx, bool isDevelopment, DateTime? expiresUtc) => new()
    {
        HttpOnly = true,
        Secure = ctx.Request.IsHttps || !isDevelopment,
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        IsEssential = true,
        Expires = expiresUtc is null ? null : new DateTimeOffset(DateTime.SpecifyKind(expiresUtc.Value, DateTimeKind.Utc))
    };
}
