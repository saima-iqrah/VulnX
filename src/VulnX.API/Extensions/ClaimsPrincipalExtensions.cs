using System.Security.Claims;
using VulnX.Application.Common;

namespace VulnX.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out var id)
            ? id
            : throw new AppException(401, "Invalid token.");
}
