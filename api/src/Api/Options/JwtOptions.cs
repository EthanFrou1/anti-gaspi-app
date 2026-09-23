using System.ComponentModel.DataAnnotations;

namespace Api.Options;

/// <summary>
/// Configuration des jetons, section « Jwt ». La clé de signature est un secret :
/// user-secrets en dev, variable d'environnement Jwt__SigningKey en prod.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    // HMAC-SHA256 exige une clé d'au moins 256 bits (32 octets).
    [Required, MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 60)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; init; } = 30;
}
