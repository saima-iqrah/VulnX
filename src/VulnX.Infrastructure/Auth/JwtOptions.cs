namespace VulnX.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "VulnX";
    public string Audience { get; set; } = "VulnX.Web";
    /// Signing key. Must come from user-secrets / environment, minimum 32 characters.
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
