using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BrainArena.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Token has no subject claim.");

        return Guid.Parse(value);
    }

    /// <summary>Null for an anonymous caller, instead of throwing — for endpoints reachable by both.</summary>
    public static Guid? GetUserIdOrNull(this ClaimsPrincipal principal) =>
        principal.Identity?.IsAuthenticated == true ? principal.GetUserId() : null;
}
