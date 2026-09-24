using System.Net;
using System.Net.Http.Json;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Api.Tests.Deployment;

/// <summary>
/// Ce qui sert au déploiement derrière Coolify : vérification de santé, en-têtes du proxy
/// (X-Forwarded-For) et migrations au démarrage.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DeploymentTests(DatabaseFixture database) : IAsyncLifetime
{
    // Réseau Docker fictif du proxy, et deux adresses : l'une dedans, l'autre dehors.
    private const string ProxyNetwork = "10.0.0.0/8";
    private const string ProxyIp = "10.0.1.5";
    private const string OutsiderIp = "203.0.113.9";

    // Limite de la politique « auth » (voir Program.cs).
    private const int AuthLimitPerMinute = 30;

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- /health ----------

    [Fact]
    public async Task Health_IsAnonymous_AndReportsHealthy()
    {
        var response = await database.Api.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_Returns503_WithoutDetails_WhenTheDatabaseIsUnreachable()
    {
        // Port 1 : rien n'y écoute, la connexion est refusée tout de suite.
        await using var api = new ApiFactory(
            "Host=127.0.0.1;Port=1;Database=absente;Username=x;Password=x;Timeout=2",
            database.OpenFoodFacts);

        var response = await api.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        // Endpoint public : ni hôte, ni message d'erreur.
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    // ---------- X-Forwarded-For ----------

    [Fact]
    public async Task ForwardedFor_FromTheTrustedProxy_GivesEachClientItsOwnRateLimit()
    {
        await using var api = BehindProxy();
        var client = api.CreateClient();

        for (var i = 0; i < AuthLimitPerMinute; i++)
        {
            await Login(client, remoteIp: ProxyIp, forwardedFor: "198.51.100.1");
        }

        // Le client 198.51.100.1 a épuisé SA limite…
        Assert.Equal(HttpStatusCode.TooManyRequests, await Login(client, ProxyIp, "198.51.100.1"));
        // …mais un autre client, derrière le même proxy, n'est pas pénalisé.
        Assert.NotEqual(HttpStatusCode.TooManyRequests, await Login(client, ProxyIp, "198.51.100.2"));
    }

    [Fact]
    public async Task ForwardedFor_FromAnUnknownSource_IsIgnored()
    {
        await using var api = BehindProxy();
        var client = api.CreateClient();

        // Un attaquant qui joint l'API sans passer par le proxy s'invente une IP à chaque essai.
        for (var i = 0; i < AuthLimitPerMinute; i++)
        {
            await Login(client, remoteIp: OutsiderIp, forwardedFor: $"198.51.100.{i + 1}");
        }

        // En-tête ignoré : toutes ses requêtes comptent pour sa vraie adresse.
        Assert.Equal(HttpStatusCode.TooManyRequests, await Login(client, OutsiderIp, "198.51.100.200"));
    }

    [Fact]
    public async Task ForwardedFor_OnlyTheAddressSeenByTheProxyCounts()
    {
        await using var api = BehindProxy();
        var client = api.CreateClient();

        // Le client ajoute de fausses adresses en tête ; Traefik ajoute la vraie à la fin.
        for (var i = 0; i < AuthLimitPerMinute; i++)
        {
            await Login(client, remoteIp: ProxyIp, forwardedFor: $"192.0.2.{i + 1}, 198.51.100.7");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, await Login(client, ProxyIp, "192.0.2.250, 198.51.100.7"));
    }

    [Fact]
    public void InvalidProxyNetwork_PreventsStartup()
    {
        using var api = database.Api.WithWebHostBuilder(builder =>
            builder.UseSetting("ReverseProxy:KnownNetworks:0", "pas-un-reseau"));

        var error = Assert.Throws<OptionsValidationException>(() => api.CreateClient());
        Assert.Contains("CIDR", error.Message);
    }

    // ---------- Migrations au démarrage ----------

    [Fact]
    public async Task MigrateOnStartup_BringsAnEmptyDatabaseUpToDate()
    {
        var connectionString = await CreateEmptyDatabaseAsync();
        try
        {
            await using var api = new ApiFactory(connectionString, database.OpenFoodFacts)
                .WithWebHostBuilder(builder => builder.UseSetting("Database:MigrateOnStartup", "true"));

            // Démarrer l'API suffit : les migrations passent avant l'ouverture du port.
            var response = await api.CreateClient().GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await using var db = new Api.Data.AppDbContext(
                new DbContextOptionsBuilder<Api.Data.AppDbContext>().UseNpgsql(connectionString).Options);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        }
        finally
        {
            await DropDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task WithoutTheOption_NothingIsMigrated()
    {
        var connectionString = await CreateEmptyDatabaseAsync();
        try
        {
            await using var api = new ApiFactory(connectionString, database.OpenFoodFacts);

            await api.CreateClient().GetAsync("/health");

            await using var db = new Api.Data.AppDbContext(
                new DbContextOptionsBuilder<Api.Data.AppDbContext>().UseNpgsql(connectionString).Options);
            Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
        }
        finally
        {
            await DropDatabaseAsync(connectionString);
        }
    }

    // ---------- Outils ----------

    /// <summary>
    /// API dont le proxy de confiance est le réseau 10.0.0.0/8. Instance séparée : sa limite
    /// de débit repart de zéro et ne gêne pas les autres tests.
    /// </summary>
    private WebApplicationFactory<Program> BehindProxy() =>
        database.Api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ReverseProxy:KnownNetworks:0", ProxyNetwork);
            builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, RemoteIpFromHeaderStartupFilter>());
        });

    private static async Task<HttpStatusCode> Login(HttpClient client, string remoteIp, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            // Corps vide : la limite de débit passe avant la validation (400), sans toucher la base.
            Content = JsonContent.Create(new { }),
        };
        request.Headers.Add(RemoteIpFromHeaderStartupFilter.Header, remoteIp);
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private async Task<string> CreateEmptyDatabaseAsync()
    {
        var name = $"migration_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await command.ExecuteNonQueryAsync();
        }
        return new NpgsqlConnectionStringBuilder(database.ConnectionString) { Database = name }.ConnectionString;
    }

    private async Task DropDatabaseAsync(string connectionString)
    {
        var name = new NpgsqlConnectionStringBuilder(connectionString).Database;
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// TestServer n'a pas de vraie connexion réseau : ce filtre, placé avant toute l'API,
    /// fixe l'adresse IP « vue » par le serveur (celle du proxy ou d'un inconnu).
    /// </summary>
    private sealed class RemoteIpFromHeaderStartupFilter : IStartupFilter
    {
        public const string Header = "X-Test-Remote-Ip";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(Header, out var ip))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
                }
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
