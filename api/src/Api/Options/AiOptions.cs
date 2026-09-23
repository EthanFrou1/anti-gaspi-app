using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public enum AiProvider
{
    // Recettes déterministes construites avec les vrais produits du foyer, sans appel externe.
    // Réservé à l'environnement Development (garde-fou au démarrage).
    Fake,

    // Vrai appel à l'API Anthropic.
    Claude,
}

/// <summary>
/// Configuration de la génération de recettes, section « Ai ».
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public AiProvider Provider { get; init; } = AiProvider.Claude;

    // Identifiant figé (snapshot) plutôt que l'alias : le comportement ne change pas
    // sans qu'on le décide. Vérifié dans la documentation Anthropic (septembre 2026).
    [Required]
    public string Model { get; init; } = "claude-haiku-4-5-20251001";

    // Recettes volontairement concises : la sortie est la partie la plus chère (5 $/M sur Haiku 4.5).
    [Range(256, 8000)]
    public int MaxOutputTokens { get; init; } = 1500;

    // Délai par tentative. Avec une nouvelle tentative, le pire cas (~2 × 25 s) reste sous
    // le délai de 60 s de l'app mobile.
    [Range(5, 50)]
    public int TimeoutSeconds { get; init; } = 25;

    [Range(0, 3)]
    public int MaxRetries { get; init; } = 1;

    [Range(1, 20)]
    public int DailyLimitPerUser { get; init; } = 3;

    // Plafond pour toute l'API : protège le budget même si beaucoup d'utilisateurs s'inscrivent.
    [Range(1, 100_000)]
    public int GlobalDailyLimit { get; init; } = 50;

    // Fuseau qui définit « aujourd'hui » pour les quotas (remise à zéro à minuit).
    [Required]
    public string TimeZone { get; init; } = "Europe/Paris";
}

/// <summary>
/// Clé de l'API Anthropic, section « Anthropic » : user-secrets en dev, variable
/// d'environnement Anthropic__ApiKey en production. Jamais dans le repo ni dans l'app mobile.
/// </summary>
public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string ApiKey { get; init; } = string.Empty;
}
