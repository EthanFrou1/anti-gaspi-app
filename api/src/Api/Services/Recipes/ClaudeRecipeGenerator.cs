using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Api.Options;
using Api.Services.Ai;
using Microsoft.Extensions.Options;

namespace Api.Services.Recipes;

/// <summary>
/// Génération par Claude (API Anthropic, SDK officiel). Avec ClaudeReceiptReader, seules
/// classes du projet qui construisent une requête Anthropic : le reste du code ne voit que
/// IRecipeGenerator.
/// </summary>
public sealed class ClaudeRecipeGenerator(
    AnthropicClient client,
    IOptions<AiOptions> options,
    ILogger<ClaudeRecipeGenerator> logger) : IRecipeGenerator
{
    public Task<RecipeDraft> GenerateAsync(RecipePrompt prompt, CancellationToken ct)
    {
        var settings = options.Value;
        var parameters = new MessageCreateParams
        {
            Model = settings.Model,
            MaxTokens = settings.MaxOutputTokens,
            // Prompt système fixe marqué pour le cache : sans effet sous le seuil minimal du
            // modèle (4 096 tokens sur Haiku 4.5), utile si l'on passe à un modèle au seuil plus bas.
            System = new List<TextBlockParam>
            {
                new() { Text = prompt.System, CacheControl = new CacheControlEphemeral() },
            },
            Messages = [new() { Role = Role.User, Content = prompt.UserContent }],
            // Format de sortie imposé : la réponse est un JSON conforme au schéma.
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat { Schema = new Dictionary<string, JsonElement>(prompt.OutputSchema) },
            },
        };

        return ClaudeStructuredCall.SendAsync<RecipeDraft>(client, parameters, logger, "Génération de recette", ct);
    }
}
