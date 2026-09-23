namespace Api.Entities;

/// <summary>
/// Une génération de recette. Sert à la fois :
/// - de réservation pour les quotas (RecipeJson null pendant l'appel à l'IA) ;
/// - d'historique (30 jours), en ne stockant QUE la recette : ni prompt, ni contraintes,
///   ni inventaire (minimisation des données).
/// </summary>
public class RecipeGeneration
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }
    public Household Household { get; set; } = null!;

    public Guid RequestedByUserId { get; set; }
    public User RequestedBy { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    // Recette validée, en JSON ; null tant que la génération est en cours.
    public string? RecipeJson { get; set; }
}
