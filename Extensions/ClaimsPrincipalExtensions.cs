using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BattleTanks.Backend.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
