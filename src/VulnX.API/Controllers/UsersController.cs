using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VulnX.API.Extensions;
using VulnX.Application.Abstractions;
using VulnX.Application.Auth;

namespace VulnX.API.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController(IAuthService auth, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<ActionResult<UserDto>> GetProfile(CancellationToken ct)
        => Ok(await auth.GetProfileAsync(User.GetUserId(), ct));

    [HttpPut("profile")]
    public async Task<ActionResult<UserDto>> UpdateProfile(UpdateProfileRequest request, CancellationToken ct)
        => Ok(await auth.UpdateProfileAsync(User.GetUserId(), request, ct));

    [HttpPost("change-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(User.GetUserId(), request, ct);
        RefreshCookie.Clear(HttpContext, env.IsDevelopment());   // all sessions were revoked
        return NoContent();
    }
}
