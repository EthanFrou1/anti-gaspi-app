using System.Text.Json;
using Api.Entities;
using Api.Services.Profiles;

namespace Api.Services.Recipes;

/// <summary>
/// Produit du frigo tel qu'il est proposé à l'IA (aucune donnée personnelle).
/// </summary>
public sealed record PromptItem(
    Guid Id,
    string Name,
    string CategoryCode,
    decimal Quantity,
    QuantityUnit Unit,
    DateOnly ExpiresOn,
    ExpiryKind ExpiryKind);

/// <summary>
/// Produit envoyé à l'IA sous une référence courte (« p1 ») : moins de tokens qu'un GUID,
/// et impossible pour l'IA d'inventer l'identifiant d'un produit hors de la liste.
/// </summary>
public sealed record PromptItemRef(string Ref, PromptItem Item, int ExpiresInDays);

/// <summary>
/// Prompt complet d'une génération : ce qui part vers l'IA, plus ce qu'il faut pour
/// valider la réponse (références des produits, contraintes).
/// </summary>
public sealed record RecipePrompt(
    string System,
    string UserContent,
    IReadOnlyDictionary<string, JsonElement> OutputSchema,
    IReadOnlyList<PromptItemRef> Items,
    MealConstraints Constraints)
{
    /// <summary>
    /// Produits du frigo écartés à cause des goûts des convives (jamais envoyés à l'IA),
    /// affichés avec la recette : « Non utilisés : champignons (préférences d'un convive) ».
    /// </summary>
    public IReadOnlyList<string> ExcludedByPreferences { get; init; } = [];

    /// <summary>
    /// Des produits du frigo ont été écartés pour une contrainte (allergène, exclusion, « Pas
    /// épicé » des seuls invités). Ni leurs noms ni la contrainte : seulement une mention
    /// générique au moment de la génération, jamais enregistrée avec la recette.
    /// </summary>
    public bool HasConstraintExclusions { get; init; }
}

/// <summary>
/// Réponse brute de l'IA (format imposé par le schéma JSON), avant validation.
/// </summary>
public sealed record RecipeDraft(
    string? Title,
    int PrepMinutes,
    int Servings,
    IReadOnlyList<RecipeDraftIngredient>? Ingredients,
    IReadOnlyList<string>? Steps);

public sealed record RecipeDraftIngredient(string? Name, string? Quantity, string? InventoryRef);

/// <summary>
/// Point d'entrée unique vers l'IA : changer de fournisseur = écrire une autre implémentation.
/// </summary>
public interface IRecipeGenerator
{
    Task<RecipeDraft> GenerateAsync(RecipePrompt prompt, CancellationToken ct);
}
