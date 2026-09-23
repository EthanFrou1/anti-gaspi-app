namespace Api.Entities;

/// <summary>
/// Refresh token persisté. Seul son hash (SHA-256) est stocké : une fuite
/// de la base ne permet pas de réutiliser les jetons.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    // Jeton émis en remplacement lors d'une rotation : permet de détecter
    // la réutilisation d'un ancien jeton (signe de vol).
    public Guid? ReplacedByTokenId { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
