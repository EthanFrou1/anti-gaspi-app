using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Api.Common;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Identifiant de l'utilisateur connecté, lu dans le claim « sub » du JWT.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Claim 'sub' absent : endpoint appelé sans authentification ?");
        return Guid.Parse(sub);
    }
}
