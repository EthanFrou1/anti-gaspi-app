using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Démarre la vraie API en mémoire (pipeline complet : authentification JWT, politiques,
/// validation des DTOs, sérialisation JSON), branchée sur la base Testcontainers.
/// Aucun port réseau n'est ouvert : les requêtes passent directement par TestServer.
/// </summary>
public sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Environnement « Testing » : pas de user-secrets (qui pointent vers la base de dev).
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Jwt:SigningKey", DatabaseTestBase.TestJwtOptions.SigningKey);
        builder.UseSetting("OpenFoodFacts:ContactEmail", "tests@example.com");
    }
}
