using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Api.Dtos.Recipes;

public sealed record GenerateRecipeRequest(
    // Membres du foyer qui mangent ; par défaut, seulement l'auteur de la demande.
    IReadOnlyList<Guid>? DinerUserIds,
    [Range(1, 12, ErrorMessage = "Le nombre de portions doit être compris entre 1 et 12.")]
    int? Servings);

public sealed record RecipeIngredientDto(string Name, string Quantity, Guid? InventoryItemId);

public sealed record RecipeDto(
    Guid Id,
    string Title,
    int PrepMinutes,
    int Servings,
    IReadOnlyList<RecipeIngredientDto> Ingredients,
    IReadOnlyList<string> Steps,
    DateTimeOffset CreatedAt);

public sealed record RecipeQuotaDto(int Used, int Limit, int Remaining, DateTimeOffset ResetsAt);

public sealed record MarkRecipeCookedRequest(
    // Produits du frigo terminés avec cette recette (cases cochées par l'utilisateur).
    [Required] IReadOnlyList<Guid> FinishedItemIds);

/// <summary>
/// Prompt exact qui serait envoyé à l'IA (endpoint de développement uniquement).
/// </summary>
public sealed record RecipePromptPreviewDto(
    string Model,
    int MaxOutputTokens,
    string System,
    string UserContent,
    JsonElement OutputSchema,
    // Tout en un seul texte, prêt à coller dans une console d'IA pour mettre au point le prompt.
    string ClipboardText);
