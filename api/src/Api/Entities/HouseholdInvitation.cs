namespace Api.Entities;

/// <summary>
/// Code d'invitation à un foyer, réutilisable jusqu'à expiration ou révocation.
/// </summary>
public class HouseholdInvitation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }
    public Household Household { get; set; } = null!;

    public required string Code { get; set; }

    // Null si l'auteur a supprimé son compte depuis.
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
