namespace Api.Entities;

/// <summary>
/// Étoile posée par un utilisateur sur une recette de son foyer. Une recette qui a au moins
/// une étoile est conservée sans limite de durée et apparaît dans le carnet commun du foyer.
/// </summary>
public class RecipeFavorite
{
    public Guid RecipeGenerationId { get; set; }
    public RecipeGeneration Recipe { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
