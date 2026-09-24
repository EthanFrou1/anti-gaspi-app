using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;

namespace Api.Services.Ai;

/// <summary>
/// Appel à Claude avec un format de sortie JSON imposé, partagé par les recettes et la
/// lecture des tickets : même traitement des erreurs, des refus et des réponses coupées.
/// Toute réponse inexploitable lève AiUnavailableException (503, quota non décompté).
/// </summary>
public static class ClaudeStructuredCall
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <param name="operation">Nom de l'opération dans les journaux (ex. « Génération de recette »).</param>
    public static async Task<T> SendAsync<T>(
        AnthropicClient client, MessageCreateParams parameters, ILogger logger, string operation, CancellationToken ct)
        where T : class
    {
        Message response;
        try
        {
            response = await client.Messages.Create(parameters, ct);
        }
        // Du plus précis au plus général : le SDK a déjà réessayé les erreurs temporaires
        // (429, 5xx, réseau) selon MaxRetries ; ici, c'est l'échec définitif.
        catch (AnthropicRateLimitException ex)
        {
            throw Unavailable(logger, operation, "limite de débit Anthropic atteinte (429)", ex);
        }
        catch (Anthropic5xxException ex)
        {
            throw Unavailable(logger, operation, "Anthropic indisponible ou surchargé (5xx)", ex);
        }
        catch (AnthropicApiException ex)
        {
            // 4xx (requête invalide, clé refusée…) : erreur de configuration à corriger.
            logger.LogError(ex, "{Operation} : requête refusée par l'API Anthropic.", operation);
            throw Unavailable(logger, operation, "requête refusée par l'API Anthropic", ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or AnthropicException
                                   && !ct.IsCancellationRequested)
        {
            throw Unavailable(logger, operation, "Anthropic injoignable ou délai dépassé", ex);
        }

        // Coût réel mesuré à chaque appel (aucune donnée personnelle dans ce journal).
        logger.LogInformation(
            "{Operation} par {Model} : {InputTokens} tokens en entrée ({CacheRead} lus en cache), {OutputTokens} en sortie, arrêt {StopReason}.",
            operation,
            parameters.Model,
            response.Usage.InputTokens,
            response.Usage.CacheReadInputTokens,
            response.Usage.OutputTokens,
            response.StopReason);

        // Décision du projet : un refus du modèle est une erreur (pas de repli sur un autre modèle).
        if (response.StopReason == StopReason.Refusal)
        {
            throw new AiUnavailableException("le modèle a refusé la demande");
        }

        if (response.StopReason == StopReason.MaxTokens)
        {
            throw new AiUnavailableException("réponse coupée (limite de tokens atteinte)");
        }

        var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
        try
        {
            return JsonSerializer.Deserialize<T>(text, JsonOptions)
                ?? throw new AiUnavailableException("réponse vide");
        }
        catch (JsonException ex)
        {
            throw new AiUnavailableException("réponse JSON illisible", ex);
        }
    }

    private static AiUnavailableException Unavailable(ILogger logger, string operation, string reason, Exception inner)
    {
        logger.LogWarning(inner, "{Operation} impossible : {Reason}.", operation, reason);
        return new AiUnavailableException(reason, inner);
    }
}
