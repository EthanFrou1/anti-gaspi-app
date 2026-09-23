using Microsoft.AspNetCore.Identity;

namespace Api.Entities;

/// <summary>
/// Utilisateur de l'application. Hérite d'IdentityUser pour bénéficier
/// du hachage des mots de passe, du verrouillage de compte, etc.
/// </summary>
public class User : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Au MVP, un utilisateur appartient à un seul foyer (null tant qu'il n'en a pas).
    public HouseholdMember? Membership { get; set; }

    public List<RefreshToken> RefreshTokens { get; set; } = [];

    // Préférences alimentaires ; null tant que l'onboarding n'est pas terminé.
    public UserProfile? Profile { get; set; }
}
