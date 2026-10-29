using VulnX.Application.Auth;

namespace VulnX.Application.Abstractions;

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthSession> RefreshAsync(string? refreshToken, CancellationToken ct);
    Task LogoutAsync(string? refreshToken, CancellationToken ct);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
    Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct);
    Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct);
    Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct);
}
