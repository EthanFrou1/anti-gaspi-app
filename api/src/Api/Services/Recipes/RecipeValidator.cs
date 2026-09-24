using Api.Services.Ai;

namespace Api.Services.Recipes;

public sealed record ValidatedIngredient(string Name, string Quantity, Guid? InventoryItemId);

public sealed record ValidatedRecipe(
    string Title,
    int PrepMinutes,
    int Servings,
    IReadOnlyList<ValidatedIngredient> Ingredients,
    IReadOnlyList<string> Steps);

/// <summary>
/// Vérifie la réponse de l'IA AVANT de l'utiliser (règle du projet). Le schéma imposé
/// garantit la forme ; ici on contrôle les bornes et la cohérence avec la demande.
/// Fonction pure. Toute anomalie rend la réponse inutilisable (503, quota non décompté).
/// </summary>
public static class RecipeValidator
{
    public const int MaxTitleLength = 120;
    public const int MaxIngredients = 20;
    public const int MaxSteps = 12;
    public const int MaxIngredientNameLength = 100;
    public const int MaxQuantityLength = 40;
    public const int MaxStepLength = 400;

    public static ValidatedRecipe Validate(RecipeDraft draft, RecipePrompt prompt)
    {
        var title = Clean(draft.Title);
        Require(title.Length is > 0 and <= MaxTitleLength, "titre vide ou trop long");

        var maxMinutes = RecipeLabels.MaxMinutes(prompt.Constraints.CookingTime);
        Require(draft.PrepMinutes >= 1 && draft.PrepMinutes <= maxMinutes,
            $"temps de préparation {draft.PrepMinutes} min hors limite ({maxMinutes} min)");

        var ingredients = draft.Ingredients ?? [];
        Require(ingredients.Count is >= 1 and <= MaxIngredients, "nombre d'ingrédients invalide");

        var refs = prompt.Items.ToDictionary(i => i.Ref, i => i.Item.Id, StringComparer.OrdinalIgnoreCase);
        var validatedIngredients = ingredients.Select(ingredient =>
        {
            var name = Clean(ingredient.Name);
            var quantity = Clean(ingredient.Quantity);
            Require(name.Length is > 0 and <= MaxIngredientNameLength, "nom d'ingrédient invalide");
            Require(quantity.Length <= MaxQuantityLength, "quantité trop longue");

            Guid? itemId = null;
            if (!string.IsNullOrWhiteSpace(ingredient.InventoryRef))
            {
                // Une référence inventée (hors de la liste envoyée) est rejetée.
                Require(refs.TryGetValue(ingredient.InventoryRef.Trim(), out var id),
                    $"référence de produit inconnue « {ingredient.InventoryRef} »");
                itemId = id;
            }
            return new ValidatedIngredient(name, quantity, itemId);
        }).ToList();

        var steps = (draft.Steps ?? []).Select(Clean).ToList();
        Require(steps.Count is >= 1 and <= MaxSteps, "nombre d'étapes invalide");
        Require(steps.All(s => s.Length is > 0 and <= MaxStepLength), "étape vide ou trop longue");

        // Le nombre de portions fait foi côté demande : on ne laisse pas l'IA le changer.
        return new ValidatedRecipe(title, draft.PrepMinutes, prompt.Constraints.Servings, validatedIngredients, steps);
    }

    private static string Clean(string? value) => (value ?? string.Empty).Trim();

    private static void Require(bool condition, string reason)
    {
        if (!condition)
        {
            throw new AiUnavailableException($"Réponse de l'IA invalide : {reason}.");
        }
    }
}
