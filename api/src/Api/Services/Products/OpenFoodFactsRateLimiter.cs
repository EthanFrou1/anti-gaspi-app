using System.Threading.RateLimiting;
using Api.Options;
using Microsoft.Extensions.Options;

namespace Api.Services.Products;

/// <summary>
/// Limite GLOBALE des appels sortants vers Open Food Facts.
///
/// OFF limite la lecture de produits par adresse IP (15 à 100 requêtes/min selon les
/// pages de sa documentation). Or tous nos appels partent de l'IP du serveur : sans cette
/// limite, quelques utilisateurs actifs suffiraient à faire bloquer l'API entière par OFF.
/// Elle s'ajoute à la limite par utilisateur (qui, elle, protège contre un seul abuseur).
///
/// Enregistré en singleton : un seul compteur partagé par toutes les requêtes.
/// </summary>
public sealed class OpenFoodFactsRateLimiter : IDisposable
{
    private readonly RateLimiter _limiter;

    public OpenFoodFactsRateLimiter(IOptions<OpenFoodFactsOptions> options)
    {
        // Fenêtre GLISSANTE découpée en 6 segments de 10 s : au plus N appels sur toute
        // période d'une minute. Une fenêtre fixe permettrait 2N appels à cheval sur deux
        // minutes (N à 12:00:59, N à 12:01:00).
        _limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = options.Value.MaxRequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            // Pas de file d'attente : mieux vaut basculer tout de suite en saisie manuelle.
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    }

    /// <summary>
    /// true si un appel à OFF est autorisé maintenant (et le décompte).
    /// </summary>
    public bool TryAcquire()
    {
        using var lease = _limiter.AttemptAcquire();
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
