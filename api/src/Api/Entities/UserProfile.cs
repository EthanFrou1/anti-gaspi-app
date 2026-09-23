namespace Api.Entities;

/// <summary>
/// Préférences alimentaires d'un utilisateur (relation 1-1). Privées : jamais montrées
/// aux autres membres du foyer ; seul le serveur les combine pour un repas partagé.
/// Son existence indique que l'onboarding est terminé.
/// </summary>
public class UserProfile
{
    // Clé primaire = clé étrangère vers l'utilisateur (relation 1-1).
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public CookingTime CookingTime { get; set; } = CookingTime.Under30Minutes;

    public MealBudget Budget { get; set; } = MealBudget.Under2Euros;

    public Diet Diet { get; set; } = Diet.Omnivore;

    public List<IngredientExclusion> Exclusions { get; set; } = [];

    // Donnée de santé (RGPD, article 9) : vide tant qu'il n'y a pas de consentement.
    public List<Allergen> Allergens { get; set; } = [];

    // Date du consentement explicite au stockage des allergies ; null = pas de consentement.
    public DateTimeOffset? HealthDataConsentAt { get; set; }

    public NutritionGoal Goal { get; set; } = NutritionGoal.SimpleAntiWaste;

    public int DefaultServings { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
