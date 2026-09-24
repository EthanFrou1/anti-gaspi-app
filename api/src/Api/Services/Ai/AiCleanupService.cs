using Api.Services.Receipts;
using Api.Services.Recipes;

namespace Api.Services.Ai;

/// <summary>
/// Tâche de fond : toutes les 6 heures, supprime ce que les fonctionnalités d'IA n'ont plus
/// besoin de garder (historique des recettes de plus de 30 jours, lectures de tickets de plus
/// de 48 heures) et les réservations abandonnées (crash pendant un appel à l'IA).
/// </summary>
public sealed class AiCleanupService(IServiceScopeFactory scopes, ILogger<AiCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                // Un scope par passage : le DbContext est « scoped », comme pour une requête HTTP.
                await using var scope = scopes.CreateAsyncScope();
                var recipes = await scope.ServiceProvider.GetRequiredService<IRecipeService>().PurgeAsync(stoppingToken);
                var receipts = await scope.ServiceProvider.GetRequiredService<IReceiptService>().PurgeAsync(stoppingToken);
                if (recipes + receipts > 0)
                {
                    logger.LogInformation(
                        "Nettoyage de l'IA : {Recipes} recette(s) et {Receipts} lecture(s) de ticket supprimées.", recipes, receipts);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Une erreur passagère (base indisponible…) ne doit pas arrêter la tâche.
                logger.LogError(ex, "Échec du nettoyage de l'IA.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
