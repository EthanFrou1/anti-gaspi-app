using Api.Data;
using DotNet.Testcontainers.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Testcontainers.PostgreSql;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Démarre UN conteneur PostgreSQL pour toute la série de tests (partagé via
/// une collection xUnit), applique les migrations une fois, puis permet de
/// remettre la base à zéro entre chaque test.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    // Même version qu'en dev (docker-compose.yml). PullPolicy.Missing : l'image locale
    // est utilisée si elle existe, aucun téléchargement n'est tenté dans ce cas.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithImagePullPolicy(PullPolicy.Missing)
        .Build();

    private string _resetSql = string.Empty;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateDbContext();
        // Applique les vraies migrations : les tests vérifient donc aussi qu'elles fonctionnent.
        await db.Database.MigrateAsync();

        // Tables de référence (catégories…) : remplies par la migration via HasData,
        // elles doivent survivre à la remise à zéro. On les déduit du modèle EF pour
        // ne pas avoir à maintenir une liste à la main.
        // Les données HasData ne figurent que dans le modèle « design-time » (celui des migrations).
        var referenceTables = db.GetService<IDesignTimeModel>().Model.GetEntityTypes()
            .Where(e => e.GetSeedData().Any())
            .Select(e => e.GetTableName())
            .OfType<string>()
            .Append("__EFMigrationsHistory")
            .ToArray();

        // Construit une seule fois la commande qui vide toutes les autres tables.
        var tables = await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT format('%I.%I', schemaname, tablename) AS "Value"
                FROM pg_tables
                WHERE schemaname = 'public' AND NOT (tablename = ANY({0}))
                """,
                [referenceTables])
            .ToListAsync();

        _resetSql = $"TRUNCATE TABLE {string.Join(", ", tables)} RESTART IDENTITY CASCADE";
    }

    public async Task ResetAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(_resetSql);
    }

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    // Toutes les classes de test marquées [Collection(DatabaseCollection.Name)]
    // partagent le conteneur et s'exécutent l'une après l'autre (pas en parallèle),
    // ce qui évite qu'un test vide la base pendant qu'un autre l'utilise.
    public const string Name = "Database";
}
