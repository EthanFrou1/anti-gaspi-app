using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Api.Services.Auth;

// Partagé entre la création (TokenService) et la validation (JwtBearer)
// pour garantir qu'on utilise exactement la même clé et le même algorithme.
public static class JwtSigningKey
{
    public const string Algorithm = SecurityAlgorithms.HmacSha256;

    public static SymmetricSecurityKey Create(string signingKey) => new(Encoding.UTF8.GetBytes(signingKey));
}
