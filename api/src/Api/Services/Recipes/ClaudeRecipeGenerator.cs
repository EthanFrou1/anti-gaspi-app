using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Api.Options;
using Microsoft.Extensions.Options;

namespace Api.Services.Recipes;

/// <summary>
/// Génération par Claude (API Anthropic, SDK officiel). Seule classe du projet qui connaît
/// le fournisseur : le reste du code ne voit que IRecipeGenerator.
/// </summary>
public sealed class ClaudeRecipeGenerator(
    AnthropicClient client,
    IOptions<AiOptions> options,
    ILogger<ClaudeRecipeGenerator> logger) : IRecipeGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RecipeDraft> GenerateAsync(RecipePrompt prompt, CancellationToken ct)
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

        Message response;
        try
        {
            response = await client.Messages.Create(parameters, ct);
        }
        // Du plus précis au plus général : le SDK a déjà réessayé les erreurs temporaires
        // (429, 5xx, réseau) selon MaxRetries ; ici, c'est l'échec définitif.
        catch (AnthropicRateLimitException ex)
        {
            throw Unavailable("limite de débit Anthropic atteinte (429)", ex);
        }
        catch (Anthropic5xxException ex)
        {
            throw Unavailable("Anthropic indisponible ou surchargé (5xx)", ex);
        }
        catch (AnthropicApiException ex)
        {
            // 4xx (requête invalide, clé refusée…) : erreur de configuration à corriger.
            logger.LogError(ex, "Requête refusée par l'API Anthropic.");
            throw Unavailable("requête refusée par l'API Anthropic", ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or AnthropicException
                                   && !ct.IsCancellationRequested)
        {
            throw Unavailable("Anthropic injoignable ou délai dépassé", ex);
        }

        // Coût réel mesuré à chaque appel (aucune donnée personnelle dans ce journal).
        logger.LogInformation(
            "Recette générée par {Model} : {InputTokens} tokens en entrée ({CacheRead} lus en cache), {OutputTokens} en sortie, arrêt {StopReason}.",
            settings.Model,
            response.Usage.InputTokens,
            response.Usage.CacheReadInputTokens,
            response.Usage.OutputTokens,
            response.StopReason);

        // Décision du projet : un refus du modèle est une erreur (pas de repli sur un autre modèle).
        if (response.StopReason == StopReason.Refusal)
        {
            throw new RecipeGenerationUnavailableException("le modèle a refusé la demande");
        }

        if (response.StopReason == StopReason.MaxTokens)
        {
            throw new RecipeGenerationUnavailableException("réponse coupée (limite de tokens atteinte)");
        }

        var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
        try
        {
            return JsonSerializer.Deserialize<RecipeDraft>(text, JsonOptions)
                ?? throw new RecipeGenerationUnavailableException("réponse vide");
        }
        catch (JsonException ex)
        {
            throw new RecipeGenerationUnavailableException("réponse JSON illisible", ex);
        }
    }

    private RecipeGenerationUnavailableException Unavailable(string reason, Exception inner)
    {
        logger.LogWarning(inner, "Génération de recette impossible : {Reason}.", reason);
        return new RecipeGenerationUnavailableException(reason, inner);
    }
}
