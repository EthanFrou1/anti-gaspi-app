using Api.Options;
using Api.Services.Recipes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Recipes;

/// <summary>
/// Garde-fou : le générateur factice ne doit jamais être actif en production,
/// et le mode Claude exige une clé hors développement.
/// </summary>
public class AiConfigurationGuardTests
{
    [Theory]
    [InlineData("Fake", null, true, AiProvider.Fake)]
    [InlineData("Claude", null, true, AiProvider.Claude)]      // dev sans clé : démarre (IA non configurée)
    [InlineData("claude", "cle", false, AiProvider.Claude)]    // insensible à la casse
    public void AllowedConfigurations(string provider, string? key, bool isDevelopment, AiProvider expected)
    {
        Assert.Equal(expected, AiConfigurationGuard.Validate(provider, key, isDevelopment));
    }

    [Theory]
    [InlineData("Fake", "cle", false)]     // Fake hors Development
    [InlineData("Claude", null, false)]    // pas de clé en production
    [InlineData("Claude", " ", false)]
    [InlineData("Manual", "cle", true)]    // mode inconnu
    [InlineData("42", "cle", true)]        // valeur numérique
    public void RejectedConfigurations(string provider, string? key, bool isDevelopment)
    {
        Assert.Throws<InvalidOperationException>(() => AiConfigurationGuard.Validate(provider, key, isDevelopment));
    }

    // Vérification au vrai démarrage de l'API, pas seulement de la fonction.
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Api_RefusesToStart_WithFakeProvider_OutsideDevelopment(string environment)
    {
        using var factory = new ConfiguredFactory(environment, "Fake", apiKey: "cle");

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Fake", Flatten(error));
    }

    [Fact]
    public void Api_RefusesToStart_InProduction_WithoutAnthropicKey()
    {
        using var factory = new ConfiguredFactory("Production", "Claude", apiKey: "");

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Anthropic", Flatten(error));
    }

    [Fact]
    public async Task Api_Starts_InDevelopment_WithFakeProvider()
    {
        using var factory = new ConfiguredFactory("Development", "Fake", apiKey: "");

        var response = await factory.CreateClient().GetAsync("/openapi/v1.json");

        Assert.True(response.IsSuccessStatusCode);
    }

    private static string Flatten(Exception error)
    {
        var messages = new List<string>();
        for (var e = error; e is not null; e = e.InnerException)
        {
            messages.Add(e.Message);
        }
        return string.Join(" | ", messages);
    }

    private sealed class ConfiguredFactory(string environment, string provider, string apiKey)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            // Valeurs factices : l'API ne se connecte à rien au démarrage.
            builder.UseSetting("ConnectionStrings:Default", "Host=localhost;Database=inutilise");
            builder.UseSetting("Jwt:SigningKey", "cle-de-signature-factice-assez-longue-pour-hmac");
            builder.UseSetting("Ai:Provider", provider);
            builder.UseSetting("Anthropic:ApiKey", apiKey);
        }
    }
}
