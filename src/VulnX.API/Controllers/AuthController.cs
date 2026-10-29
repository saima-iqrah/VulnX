using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VulnX.API.Extensions;
using VulnX.Application.Abstractions;
using VulnX.Application.Auth;

namespace VulnX.API.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(IAuthService auth, IWebHostEnvironment env) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest request, CancellationToken ct)
        => Created("/api/auth/me", await auth.RegisterAsync(request, ct));

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var session = await auth.LoginAsync(request, ct);
        RefreshCookie.Set(HttpContext, session.RefreshToken, session.RefreshTokenExpiresAtUtc, env.IsDevelopment());
        return Ok(session.Response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        // No cookie = the visitor is simply not logged in. This happens on every first page load,
        // so answer 401 directly instead of throwing an exception.
        var cookie = Request.Cookies[RefreshCookie.Name];
        if (string.IsNullOrWhiteSpace(cookie))
            return Unauthorized();

        var session = await auth.RefreshAsync(cookie, ct);
        RefreshCookie.Set(HttpContext, session.RefreshToken, session.RefreshTokenExpiresAtUtc, env.IsDevelopment());
        return Ok(session.Response);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookie.Name], ct);
        RefreshCookie.Clear(HttpContext, env.IsDevelopment());
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await auth.ForgotPasswordAsync(request, ct);
        return Ok(new { message = "If an account with that email exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await auth.ResetPasswordAsync(request, ct);
        RefreshCookie.Clear(HttpContext, env.IsDevelopment());
        return NoContent();
    }

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken ct)
    {
        await auth.ConfirmEmailAsync(request, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
        => Ok(await auth.GetProfileAsync(User.GetUserId(), ct));
}