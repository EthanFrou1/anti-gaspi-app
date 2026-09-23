using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Dtos.Auth;
using Api.Dtos.Products;
using Api.Services.Auth;
using Api.Services.Products;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.EndToEnd;

/// <summary>
/// Recherche par code-barres à travers toute l'API. Open Food Facts est simulé :
/// chaque test utilise ses propres codes-barres, car le cache de l'API est partagé.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ProductsEndpointsTests(DatabaseFixture database) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task KnownBarcode_Returns200WithSuggestion()
    {
        database.OpenFoodFacts.Add(FakeOpenFoodFactsClient.Skyr("3033490004001"));
        using var client = await AuthenticatedClientAsync();

        var response = await client.GetAsync("/api/products/barcode/3033490004001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var suggestion = await response.Content.ReadFromJsonAsync<ProductSuggestionDto>(Json);
        Assert.Equal("Skyr", suggestion!.Name);
        Assert.Equal("Open Food Facts", suggestion.Source);
    }

    [Fact]
    public async Task UnknownBarcode_Returns404WithCode()
    {
        using var client = await AuthenticatedClientAsync();

        var response = await client.GetAsync("/api/products/barcode/3000000000999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("product.not_found", await ReadCodeAsync(response));
    }

    [Fact]
    public async Task OpenFoodFactsOutage_Returns503WithCode()
    {
        database.OpenFoodFacts.MakeUnavailable("3000000000888");
        using var client = await AuthenticatedClientAsync();

        var response = await client.GetAsync("/api/products/barcode/3000000000888");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("product.lookup_unavailable", await ReadCodeAsync(response));
    }

    [Theory]
    [InlineData("abc12345")]         // lettres
    [InlineData("1234567")]          // trop court
    [InlineData("123456789012345")]  // trop long
    [InlineData("..%2F..%2Fadmin")]  // tentative de traversée de chemin
    public async Task InvalidBarcode_NeverReachesOpenFoodFacts(string barcode)
    {
        using var client = await AuthenticatedClientAsync();
        var callsBefore = database.OpenFoodFacts.Calls;

        var response = await client.GetAsync($"/api/products/barcode/{barcode}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(callsBefore, database.OpenFoodFacts.Calls);
    }

    [Fact]
    public async Task WithoutToken_Returns401()
    {
        using var anonymous = database.Api.CreateClient();

        var response = await anonymous.GetAsync("/api/products/barcode/3033490004743");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        using var scope = database.Api.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAuthService>().RegisterAsync(
            new RegisterRequest($"scan-{Guid.NewGuid():N}@example.com", DatabaseTestBase.DefaultPassword, "Scan"),
            CancellationToken.None);

        var client = database.Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.Value!.AccessToken);
        return client;
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString();
}
