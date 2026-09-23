using System.ComponentModel.DataAnnotations;

namespace Api.Options;

/// <summary>
/// Configuration d'Open Food Facts, section « OpenFoodFacts ».
/// </summary>
public sealed class OpenFoodFactsOptions
{
    public const string SectionName = "OpenFoodFacts";

    [Required, Url]
    public string BaseUrl { get; init; } = "https://world.openfoodfacts.org";

    // Contact exigé par OFF dans le User-Agent, pour pouvoir prévenir en cas d'abus.
    // Hors du repo (public) : user-secrets en dev, variable d'environnement en prod.
    public string ContactEmail { get; init; } = string.Empty;

    [Range(1, 30)]
    public int TimeoutSeconds { get; init; } = 5;

    // Limite globale des appels sortants (toute l'API partage la même IP auprès d'OFF).
    // Calée sur la valeur la plus stricte de la documentation d'OFF.
    [Range(1, 100)]
    public int MaxRequestsPerMinute { get; init; } = 15;
}
