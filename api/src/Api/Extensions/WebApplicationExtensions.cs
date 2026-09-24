using Api.Data;
using Api.Options;
using Microsoft.EntityFrameworkCore;

namespace Api.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Applique les migrations EF en attente si Database:MigrateOnStartup est activé
    /// (production). Appelé avant app.Run() : le port n'est ouvert qu'une fois la base à jour.
    /// Une erreur fait échouer le démarrage, volontairement (voir DatabaseOptions).
    /// </summary>
    public static async Task MigrateDatabaseIfEnabledAsync(this WebApplication app)
    {
        var options = app.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        if (!options.MigrateOnStartup)
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            app.Logger.LogInformation("Base de données à jour : aucune migration à appliquer.");
            return;
        }

        app.Logger.LogInformation("Application de {Count} migration(s) : {Migrations}", pending.Count, string.Join(", ", pending));
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Migrations appliquées.");
    }
}
