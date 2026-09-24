namespace Api.Options;

/// <summary>
/// Gestion de la base au démarrage, section « Database ».
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Applique les migrations EF avant d'ouvrir le port (à activer en production).
    /// EF Core pose un verrou exclusif pendant l'opération : deux instances qui
    /// démarrent en même temps ne migrent pas deux fois. Si une migration
    /// échoue, l'API s'arrête, la vérification de santé échoue et Coolify garde l'ancienne
    /// version. Désactivé par défaut : en développement, le script de dev passe par dotnet ef.
    /// </summary>
    public bool MigrateOnStartup { get; init; }
}
