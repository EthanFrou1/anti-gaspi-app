using Api.Services.Ai;

namespace Api.Services.Recipes;

/// <summary>
/// Utilisé en développement quand Ai:Provider = Claude mais que la clé Anthropic n'est pas
/// renseignée : le reste de l'API fonctionne, la génération répond 503 « IA non configurée ».
/// (En production, cette situation empêche le démarrage : voir AiConfigurationGuard.)
/// </summary>
public sealed class UnconfiguredRecipeGenerator : IRecipeGenerator
{
    public Task<RecipeDraft> GenerateAsync(RecipePrompt prompt, CancellationToken ct) =>
        throw new AiUnavailableException(
            "IA non configurée : renseigne la clé Anthropic:ApiKey dans les user-secrets.");
}
