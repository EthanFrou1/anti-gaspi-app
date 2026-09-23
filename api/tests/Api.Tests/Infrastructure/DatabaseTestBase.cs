using Api.Data;
using Api.Extensions;
using Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Classe de base des tests qui utilisent la base de données.
/// Chaque test démarre sur une base vide, avec les mêmes services que l'API.
/// </summary>
[Collection(DatabaseCollection.Name)]
public abstract class DatabaseTestBase(DatabaseFixture database) : IAsyncLifetime
{
    public static readonly JwtOptions TestJwtOptions = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "clé-de-test-suffisamment-longue-pour-hmac-sha256",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 30,
    };

    private ServiceProvider _services = null!;

    // Horloge fixée à l'heure réelle au début du test, avançable ensuite.
    protected TestClock Clock { get; } = new(DateTimeOffset.UtcNow);

    public async Task InitializeAsync()
    {
        await database.ResetAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(database.ConnectionString));
        services.AddAppIdentity();
        services.AddApplicationServices();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(TestJwtOptions));
        // Enregistré après AddApplicationServices : c'est la dernière inscription qui gagne.
        services.AddSingleton<TimeProvider>(Clock);

        _services = services.BuildServiceProvider(validateScopes: true);
    }

    public async Task DisposeAsync() => await _services.DisposeAsync();

    /// <summary>
    /// Crée un scope, comme le fait ASP.NET Core pour chaque requête HTTP :
    /// un nouveau DbContext, donc aucune donnée en cache d'un appel à l'autre.
    /// </summary>
    protected AsyncServiceScope CreateScope() => _services.CreateAsyncScope();

    protected async Task<T> WithServiceAsync<TService, T>(Func<TService, Task<T>> action)
        where TService : notnull
    {
        await using var scope = CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    protected async Task WithServiceAsync<TService>(Func<TService, Task> action)
        where TService : notnull
    {
        await using var scope = CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    protected AppDbContext CreateDbContext() => database.CreateDbContext();
}
