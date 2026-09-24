using Api.Common;
using Api.Data;
using Api.Dtos.Auth;
using Api.Extensions;
using Api.Services.Auth;
using Api.Services.Receipts;
using Api.Services.Recipes;
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

    // Générateur de recettes pilotable (jamais d'appel à une vraie IA dans les tests).
    protected TestRecipeGenerator Generator { get; } = new();

    // Lecteur de tickets pilotable, lui aussi sans appel à une vraie IA.
    protected TestReceiptReader ReceiptReader { get; } = new();

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
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new Api.Options.AiOptions()));
        services.AddSingleton<IRecipeGenerator>(Generator);
        services.AddSingleton<IReceiptReader>(ReceiptReader);
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

    public const string DefaultPassword = "un-mot-de-passe-solide";

    /// <summary>
    /// Crée un utilisateur via le vrai service d'inscription et renvoie son identifiant.
    /// </summary>
    protected async Task<Guid> CreateUserAsync(string displayName)
    {
        var email = $"{displayName.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";
        var result = await WithServiceAsync<IAuthService, Result<AuthResponse>>(s =>
            s.RegisterAsync(new RegisterRequest(email, DefaultPassword, displayName), CancellationToken.None));

        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value.User.Id;
    }
}
