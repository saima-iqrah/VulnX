using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using VulnX.Application.Abstractions;
using VulnX.Application.Auth;
using VulnX.Application.Common;
using VulnX.Domain.Common;
using VulnX.Domain.Entities;
using VulnX.Domain.Enums;
using VulnX.Infrastructure.Identity;
using VulnX.Infrastructure.Persistence;

namespace VulnX.Infrastructure.Auth;

internal sealed class AuthService(
    UserManager<ApplicationUser> users,
    AppDbContext db,
    ITokenService tokens,
    IOptions<JwtOptions> jwtOptions,
    IAppEmailSender email,
    IAuditLogger audit,
    IConfiguration config,
    ISystemSettings settings) : IAuthService
{
    private const string InvalidCredentials = "Invalid email or password.";

    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (!await settings.GetBoolAsync("registration.enabled", true, ct))
            throw new AppException(403, "Registration is currently closed.");

        var address = request.Email.Trim();
        if (await users.FindByEmailAsync(address) is not null)
            throw new AppException(409, "An account with this email already exists.");

        var user = new ApplicationUser
        {
            UserName = address,
            Email = address,
            FullName = request.FullName.Trim(),
            EmailConfirmed = false,
            Status = AccountStatus.Active
        };

        ThrowIfFailed(await users.CreateAsync(user, request.Password));
        ThrowIfFailed(await users.AddToRoleAsync(user, Roles.User));

        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var link = FrontendUrl("/verify-email", ("email", address), ("token", token));
        await email.SendAsync(address, "Verify your VulnX account",
            $"<p>Welcome to VulnX. Verify your email: <a href=\"{link}\">{link}</a></p>", ct);

        await audit.LogAsync(user.Id, "auth.register", "User", user.Id.ToString(), null, ct);
        return await ToDtoAsync(user);
    }

    public async Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim())
                   ?? throw new AppException(401, InvalidCredentials);

        if (await users.IsLockedOutAsync(user))
            throw new AppException(423, "Account temporarily locked after too many failed attempts. Try again later.");

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            await audit.LogAsync(user.Id, "auth.login_failed", "User", user.Id.ToString(), null, ct);
            throw new AppException(401, InvalidCredentials);
        }

        // Status is checked only after the password is correct so it can't be probed anonymously.
        if (user.Status == AccountStatus.Suspended)
            throw new AppException(403, "This account has been suspended.");
        if (user.Status == AccountStatus.Locked)
            throw new AppException(403, "This account has been locked by an administrator.");

        if (RequireConfirmedEmail() && !user.EmailConfirmed)
            throw new AppException(403, "Please verify your email address before signing in.");

        await users.ResetAccessFailedCountAsync(user);
        user.LastLoginAtUtc = DateTime.UtcNow;
        await users.UpdateAsync(user);

        await audit.LogAsync(user.Id, "auth.login", "User", user.Id.ToString(), null, ct);
        return await IssueSessionAsync(user, ct);
    }

    public async Task<AuthSession> RefreshAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new AppException(401, "Missing refresh token.");

        var hash = tokens.Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
                     ?? throw new AppException(401, "Invalid refresh token.");

        if (stored.RevokedAtUtc is not null)
        {
            // A revoked token being presented again suggests theft: kill every session of this user.
            await RevokeAllAsync(stored.UserId, ct);
            await audit.LogAsync(stored.UserId, "auth.refresh_reuse_detected", "User", stored.UserId.ToString(), null, ct);
            throw new AppException(401, "Invalid refresh token.");
        }

        if (stored.ExpiresAtUtc <= DateTime.UtcNow)
            throw new AppException(401, "Refresh token expired.");

        var user = await users.FindByIdAsync(stored.UserId.ToString());
        if (user is null || user.Status != AccountStatus.Active)
        {
            await RevokeAllAsync(stored.UserId, ct);
            throw new AppException(401, "Session is no longer valid.");
        }

        stored.RevokedAtUtc = DateTime.UtcNow;   // rotation: old token dies, new one is issued
        return await IssueSessionAsync(user, ct);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;

        var hash = tokens.Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || stored.RevokedAtUtc is not null) return;

        stored.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(stored.UserId, "auth.logout", "User", stored.UserId.ToString(), null, ct);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        // Always behaves the same to the caller so accounts can't be enumerated.
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.Status == AccountStatus.Suspended) return;

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var link = FrontendUrl("/reset-password", ("email", user.Email!), ("token", token));
        await email.SendAsync(user.Email!, "Reset your VulnX password",
            $"<p>Reset your password: <a href=\"{link}\">{link}</a>. If you did not request this, ignore this email.</p>", ct);
        await audit.LogAsync(user.Id, "auth.password_reset_requested", "User", user.Id.ToString(), null, ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim())
                   ?? throw new AppException(400, "Invalid or expired reset token.");

        ThrowIfFailed(await users.ResetPasswordAsync(user, request.Token, request.NewPassword));

        await RevokeAllAsync(user.Id, ct);   // sensitive event: invalidate all sessions
        await audit.LogAsync(user.Id, "auth.password_reset", "User", user.Id.ToString(), null, ct);
    }

    public async Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim())
                   ?? throw new AppException(400, "Invalid or expired verification token.");

        ThrowIfFailed(await users.ConfirmEmailAsync(user, request.Token));
        await audit.LogAsync(user.Id, "auth.email_confirmed", "User", user.Id.ToString(), null, ct);
    }

    public async Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString())
                   ?? throw new AppException(404, "User not found.");
        return await ToDtoAsync(user);
    }

    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString())
                   ?? throw new AppException(404, "User not found.");

        user.FullName = request.FullName.Trim();
        ThrowIfFailed(await users.UpdateAsync(user));
        await audit.LogAsync(user.Id, "user.profile_updated", "User", user.Id.ToString(), null, ct);
        return await ToDtoAsync(user);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString())
                   ?? throw new AppException(404, "User not found.");

        ThrowIfFailed(await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword));

        await RevokeAllAsync(user.Id, ct);   // sensitive event: invalidate all sessions
        await audit.LogAsync(user.Id, "auth.password_changed", "User", user.Id.ToString(), null, ct);
    }

    // ---------- helpers ----------

    private async Task<AuthSession> IssueSessionAsync(ApplicationUser user, CancellationToken ct)
    {
        var roles = await users.GetRolesAsync(user);
        var access = tokens.CreateAccessToken(user, roles);

        var raw = tokens.GenerateRefreshToken();
        var refreshExpires = DateTime.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays);
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokens.Hash(raw),
            ExpiresAtUtc = refreshExpires
        });
        await db.SaveChangesAsync(ct);   // also persists a rotated (revoked) token, if any

        var dto = await ToDtoAsync(user);
        return new AuthSession(new AuthResponse(access.Token, access.ExpiresAtUtc, dto), raw, refreshExpires);
    }

    private Task RevokeAllAsync(Guid userId, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, (DateTime?)DateTime.UtcNow), ct);

    private async Task<UserDto> ToDtoAsync(ApplicationUser user)
    {
        var roles = (await users.GetRolesAsync(user)).ToList();
        return new UserDto(user.Id, user.Email ?? string.Empty, user.FullName, roles,
            user.Status.ToString(), user.EmailConfirmed, user.CreatedAtUtc, user.LastLoginAtUtc);
    }

    private bool RequireConfirmedEmail() =>
        bool.TryParse(config["Auth:RequireConfirmedEmail"], out var v) && v;

    private string FrontendUrl(string path, params (string Key, string Value)[] query)
    {
        var baseUrl = (config["Frontend:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var qs = string.Join("&", query.Select(q => $"{q.Key}={Uri.EscapeDataString(q.Value)}"));
        return $"{baseUrl}{path}?{qs}";
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new AppException(400, string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}


