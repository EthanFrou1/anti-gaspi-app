namespace Api.Services.Recipes;

/// <summary>
/// Générateur factice, déterministe et gratuit : il construit une recette avec les vrais
/// produits du foyer (les plus urgents, déjà filtrés selon les contraintes par le
/// RecipePromptBuilder). Utilisé par les tests et en développement ; interdit ailleurs
/// (AiConfigurationGuard).
/// </summary>
public sealed class FakeRecipeGenerator : IRecipeGenerator
{
    public Task<RecipeDraft> GenerateAsync(RecipePrompt prompt, CancellationToken ct)
    {
        var used = prompt.Items.Take(3).ToList();
        var names = used.Select(i => i.Item.Name.ToLowerInvariant()).ToList();

        var title = names.Count == 0
            ? "Omelette du placard"
            : $"Poêlée anti-gaspi : {JoinFrench(names)}";
        if (title.Length > 60)
        {
            title = title[..57] + "...";
        }

        var ingredients = used
            .Select(i => new RecipeDraftIngredient(i.Item.Name, "tout", i.Ref))
            .Append(new RecipeDraftIngredient("Huile", "1 c. à soupe", null))
            .Append(new RecipeDraftIngredient("Sel, poivre", "selon le goût", null))
            .ToList();

        IReadOnlyList<string> steps =
        [
            "Coupe les ingrédients en morceaux de taille similaire.",
            "Fais-les revenir 10 minutes à feu moyen dans l'huile, en remuant.",
            "Assaisonne, goûte et sers chaud.",
        ];

        var maxMinutes = RecipeLabels.MaxMinutes(prompt.Constraints.CookingTime);
        var draft = new RecipeDraft(title, Math.Min(15, maxMinutes), prompt.Constraints.Servings, ingredients, steps);
        return Task.FromResult(draft);
    }

    private static string JoinFrench(IReadOnlyList<string> values) => values.Count switch
    {
        1 => values[0],
        _ => $"{string.Join(", ", values.Take(values.Count - 1))} et {values[^1]}",
    };
}
