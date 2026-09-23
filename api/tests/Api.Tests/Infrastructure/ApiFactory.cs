using Api.Services.Products;
using Api.Services.Recipes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Démarre la vraie API en mémoire (pipeline complet : authentification JWT, politiques,
/// validation des DTOs, sérialisation JSON), branchée sur la base Testcontainers.
/// Aucun port réseau n'est ouvert : les requêtes passent directement par TestServer.
/// </summary>
public sealed class ApiFactory(string connectionString, FakeOpenFoodFactsClient openFoodFacts)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Environnement « Testing » : pas de user-secrets (qui pointent vers la base de dev).
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Jwt:SigningKey", DatabaseTestBase.TestJwtOptions.SigningKey);
        builder.UseSetting("OpenFoodFacts:ContactEmail", "tests@example.com");

        // Garde-fou : hors Development, seul le mode Claude est autorisé, avec une clé. On le
        // déclare donc avec une clé factice… puis on remplace le générateur par le faux :
        // aucun test ne peut appeler la vraie API ni consommer de crédits.
        builder.UseSetting("Ai:Provider", "Claude");
        builder.UseSetting("Anthropic:ApiKey", "cle-factice-jamais-utilisee");

        // Open Food Facts remplacé par un faux : les tests ne dépendent pas d'internet.
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IOpenFoodFactsClient>(openFoodFacts);
            services.AddSingleton<IRecipeGenerator, FakeRecipeGenerator>();
        });
    }
}
