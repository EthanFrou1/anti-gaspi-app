namespace Api.Services.Recipes;

/// <summary>
/// Tâche de fond : toutes les 6 heures, supprime l'historique de plus de 30 jours
/// et les réservations abandonnées (crash pendant un appel à l'IA).
/// </summary>
public sealed class RecipeCleanupService(IServiceScopeFactory scopes, ILogger<RecipeCleanupService> logger) : BackgroundService
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
                var deleted = await scope.ServiceProvider.GetRequiredService<IRecipeService>().PurgeAsync(stoppingToken);
                if (deleted > 0)
                {
                    logger.LogInformation("Nettoyage des recettes : {Count} entrées supprimées.", deleted);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Une erreur passagère (base indisponible…) ne doit pas arrêter la tâche.
                logger.LogError(ex, "Échec du nettoyage des recettes.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
