using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Api.Entities;
using Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Api.Services.Auth;

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider time) : ITokenService
{
    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateAccessToken(User user)
    {
        var now = time.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            // Volontairement minimal : l'identifiant suffit, le reste se lit en base.
            // Un JWT est lisible par quiconque le possède (il est signé, pas chiffré).
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
            SigningCredentials = new SigningCredentials(
                JwtSigningKey.Create(_options.SigningKey), JwtSigningKey.Algorithm),
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }

    public GeneratedRefreshToken CreateRefreshToken()
    {
        // 256 bits aléatoires issus d'un générateur cryptographique.
        var rawToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return new GeneratedRefreshToken(rawToken, HashRefreshToken(rawToken));
    }

    // SHA-256 simple (sans sel ni bcrypt) : le jeton est déjà totalement aléatoire,
    // il est donc impossible à deviner par force brute. Un hash rapide et déterministe
    // permet en plus de le retrouver en base via un index.
    public string HashRefreshToken(string rawToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
