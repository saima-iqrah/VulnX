using System.ComponentModel.DataAnnotations;

namespace VulnX.Application.Auth;

public sealed class RegisterRequest
{
    [Required, StringLength(200, MinimumLength = 2)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 10)] public string Password { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(128)] public string Password { get; set; } = string.Empty;
}

public sealed class ForgotPasswordRequest
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(2048)] public string Token { get; set; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 10)] public string NewPassword { get; set; } = string.Empty;
}

public sealed class ConfirmEmailRequest
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(2048)] public string Token { get; set; } = string.Empty;
}

public sealed class UpdateProfileRequest
{
    [Required, StringLength(200, MinimumLength = 2)] public string FullName { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required, StringLength(128)] public string CurrentPassword { get; set; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 10)] public string NewPassword { get; set; } = string.Empty;
}

public sealed record UserDto(
    Guid Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    string Status,
    bool EmailConfirmed,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc);

/// Returned to the client in the JSON body. The refresh token is NOT here; it travels in an HttpOnly cookie.
public sealed record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, UserDto User);

/// Internal result so the API layer can set the refresh-token cookie.
public sealed record AuthSession(AuthResponse Response, string RefreshToken, DateTime RefreshTokenExpiresAtUtc);
