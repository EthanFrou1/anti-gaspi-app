using Api.Options;

namespace Api.Services.Recipes;

/// <summary>
/// Garde-fou exécuté au démarrage : l'API refuse de démarrer avec une configuration d'IA
/// dangereuse ou incomplète, plutôt que de le découvrir en production.
/// </summary>
public static class AiConfigurationGuard
{
    /// <summary>
    /// Lit « Ai:Provider » et vérifie la cohérence avec l'environnement.
    /// Lève InvalidOperationException (démarrage interrompu) en cas de problème.
    /// </summary>
    public static AiProvider Validate(string? providerSetting, string? anthropicApiKey, bool isDevelopment)
    {
        if (!Enum.TryParse<AiProvider>(providerSetting, ignoreCase: true, out var provider)
            || !Enum.IsDefined(provider))
        {
            throw new InvalidOperationException(
                $"Ai:Provider « {providerSetting} » inconnu. Valeurs possibles : {string.Join(", ", Enum.GetNames<AiProvider>())}.");
        }

        // Le générateur factice ne doit JAMAIS servir de vraies recettes en production.
        if (provider == AiProvider.Fake && !isDevelopment)
        {
            throw new InvalidOperationException(
                "Ai:Provider = Fake est réservé à l'environnement Development. Configurez Ai:Provider = Claude.");
        }

        // En production, une clé absente est une erreur de déploiement : on échoue tout de suite.
        // En développement, on démarre quand même (la génération répondra « IA non configurée »).
        if (provider == AiProvider.Claude && string.IsNullOrWhiteSpace(anthropicApiKey) && !isDevelopment)
        {
            throw new InvalidOperationException(
                "Ai:Provider = Claude exige la clé Anthropic:ApiKey (variable d'environnement Anthropic__ApiKey).");
        }

        return provider;
    }
}
