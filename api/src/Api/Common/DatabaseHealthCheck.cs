using Api.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Common;

/// <summary>
/// Vérification de santé (endpoint /health) : l'API répond ET joint sa base.
/// Coolify s'en sert pendant un déploiement : une nouvelle version qui ne joint pas la
/// base n'est pas mise en service, l'ancienne reste en place.
/// Écrite à la main pour ne pas ajouter de paquet (une ligne suffit).
/// </summary>
public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            // Aucun détail (hôte, erreur) : l'endpoint est public.
            : HealthCheckResult.Unhealthy();
}
