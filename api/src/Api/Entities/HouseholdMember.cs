namespace Api.Entities;

/// <summary>
/// Table de liaison entre un utilisateur et un foyer, qui porte le rôle.
/// Clé primaire composite (HouseholdId, UserId).
/// </summary>
public class HouseholdMember
{
    public Guid HouseholdId { get; set; }
    public Household Household { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public HouseholdRole Role { get; set; } = HouseholdRole.Member;

    // Sert à désigner le membre le plus ancien lors d'un transfert de propriété.
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}
