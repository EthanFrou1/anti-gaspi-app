using Api.Common;
using Api.Data.Seed;
using Api.Dtos.Products;
using Api.Entities;
using Api.Services.Inventory;
using Api.Services.Products;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Products;

public class ProductLookupServiceTests(DatabaseFixture database) : DatabaseTestBase(database)
{
    private readonly FakeOpenFoodFactsClient _openFoodFacts = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    [Fact]
    public async Task KnownProduct_IsTurnedIntoASuggestion()
    {
        _openFoodFacts.Add(FakeOpenFoodFactsClient.Skyr());

        var result = await LookupAsync("3033490004743");

        Assert.True(result.IsSuccess);
        Assert.Equal("Skyr", result.Value.Name);
        Assert.Equal("DANONE PRODUITS FRAIS FRANCE", result.Value.Brand);
        // Le skyr est étiqueté « fromages » ET « yaourts » : l'étiquette la plus précise l'emporte.
        Assert.Equal(CategorySeed.Categories.Single(c => c.Code == "yogurts").Id, result.Value.CategoryId);
        Assert.Equal(140m, result.Value.Quantity);
        Assert.Equal(QuantityUnit.Gram, result.Value.Unit);
        Assert.Equal("Open Food Facts", result.Value.Source);
    }

    [Fact]
    public async Task FirstBrand_IsKept()
    {
        _openFoodFacts.Add(new OffProduct("3017620422003", "Nutella", "Nutella, Ferrero", [], 400, "g", null));

        var result = await LookupAsync("3017620422003");

        Assert.Equal("Nutella", result.Value!.Brand);
    }

    [Fact]
    public async Task ProductWithoutKnownCategory_HasNoCategory()
    {
        _openFoodFacts.Add(new OffProduct("3000000000002", "Produit exotique", null, ["en:unknown"], null, null, null));

        var result = await LookupAsync("3000000000002");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.CategoryId);
    }

    [Fact]
    public async Task SameBarcode_IsServedFromCache()
    {
        _openFoodFacts.Add(FakeOpenFoodFactsClient.Skyr());

        await LookupAsync("3033490004743");
        await LookupAsync("3033490004743");

        Assert.Equal(1, _openFoodFacts.Calls);
    }

    [Fact]
    public async Task UnknownProduct_ReturnsNotFound_AndIsCachedToo()
    {
        var first = await LookupAsync("3000000000003");
        var second = await LookupAsync("3000000000003");

        Assert.Equal(ProductLookupErrors.NotFound, first.Error);
        Assert.Equal(ProductLookupErrors.NotFound, second.Error);
        Assert.Equal(1, _openFoodFacts.Calls);
    }

    [Fact]
    public async Task Outage_ReturnsUnavailable_AndIsNotCached()
    {
        var barcode = "3033490004743";
        _openFoodFacts.Add(FakeOpenFoodFactsClient.Skyr(barcode));
        _openFoodFacts.MakeUnavailable(barcode);

        var duringOutage = await LookupAsync(barcode);
        _openFoodFacts.MakeAvailable(barcode);
        var afterOutage = await LookupAsync(barcode);

        Assert.Equal(ProductLookupErrors.Unavailable, duringOutage.Error);
        // Une panne n'est pas mémorisée : le scan suivant réessaie.
        Assert.True(afterOutage.IsSuccess);
    }

    [Fact]
    public async Task GlobalLimitReached_ReturnsUnavailable_ButCachedProductsAreStillServed()
    {
        // Vrai client OFF (faux serveur derrière), limité à 1 appel par minute.
        var (realClient, handler) = OpenFoodFactsClientTests.CreateClient(
            _ => Task.FromResult(OpenFoodFactsClientTests.Json(System.Net.HttpStatusCode.OK, OpenFoodFactsClientTests.SkyrJson)),
            limiter: OpenFoodFactsClientTests.Limiter(1));

        var first = await LookupAsync("3033490004743", realClient);
        var otherProduct = await LookupAsync("3017620422003", realClient);
        var sameProductAgain = await LookupAsync("3033490004743", realClient);

        Assert.True(first.IsSuccess);
        // Limite atteinte : l'API répondra 503 et l'app basculera en saisie manuelle.
        Assert.Equal(ProductLookupErrors.Unavailable, otherProduct.Error);
        // Un produit déjà en cache ne consomme pas le quota.
        Assert.True(sameProductAgain.IsSuccess);
        Assert.Equal(1, handler.Calls);
    }

    private async Task<Result<ProductSuggestionDto>> LookupAsync(string barcode, IOpenFoodFactsClient client)
    {
        await using var scope = CreateScope();
        var service = new ProductLookupService(
            client, scope.ServiceProvider.GetRequiredService<ICategoryService>(), _cache);
        return await service.LookupAsync(barcode, CancellationToken.None);
    }

    private async Task<Result<ProductSuggestionDto>> LookupAsync(string barcode)
    {
        await using var scope = CreateScope();
        var service = new ProductLookupService(
            _openFoodFacts, scope.ServiceProvider.GetRequiredService<ICategoryService>(), _cache);
        return await service.LookupAsync(barcode, CancellationToken.None);
    }
}
