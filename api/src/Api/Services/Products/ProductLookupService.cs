using Api.Common;
using Api.Dtos.Products;
using Api.Services.Inventory;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Services.Products;

public static class ProductLookupErrors
{
    public static readonly Error NotFound = new(
        ErrorType.NotFound, "product.not_found", "Produit inconnu d'Open Food Facts. Tu peux le saisir à la main.");

    public static readonly Error Unavailable = new(
        ErrorType.Unavailable, "product.lookup_unavailable",
        "La recherche par code-barres est momentanément indisponible. Tu peux saisir le produit à la main.");
}

public interface IProductLookupService
{
    Task<Result<ProductSuggestionDto>> LookupAsync(string barcode, CancellationToken ct);
}

/// <summary>
/// Recherche d'un produit par code-barres, avec cache : un même code scanné par
/// plusieurs utilisateurs ne coûte qu'un appel à Open Food Facts.
/// </summary>
public sealed class ProductLookupService(
    IOpenFoodFactsClient client,
    ICategoryService categories,
    IMemoryCache cache) : IProductLookupService
{
    public const string SourceName = "Open Food Facts";

    // Les fiches OFF évoluent peu : un jour de cache suffit. Un produit inconnu peut être
    // ajouté à OFF entre-temps, d'où un délai plus court.
    public static readonly TimeSpan FoundCacheDuration = TimeSpan.FromHours(24);
    public static readonly TimeSpan NotFoundCacheDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan RulesCacheDuration = TimeSpan.FromHours(1);

    private const string RulesCacheKey = "off:category-rules";

    public async Task<Result<ProductSuggestionDto>> LookupAsync(string barcode, CancellationToken ct)
    {
        var cacheKey = $"off:product:{barcode}";
        if (cache.TryGetValue(cacheKey, out ProductSuggestionDto? cached))
        {
            // Une entrée nulle en cache signifie « inconnu d'OFF ».
            return cached is null ? ProductLookupErrors.NotFound : cached;
        }

        OffProduct? product;
        try
        {
            product = await client.GetProductAsync(barcode, ct);
        }
        catch (OpenFoodFactsUnavailableException)
        {
            // Pas de mise en cache : on réessaiera au prochain scan.
            return ProductLookupErrors.Unavailable;
        }

        if (product is null)
        {
            cache.Set<ProductSuggestionDto?>(cacheKey, null, NotFoundCacheDuration);
            return ProductLookupErrors.NotFound;
        }

        var rules = await cache.GetOrCreateAsync(RulesCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = RulesCacheDuration;
            return categories.GetOffRulesAsync(ct);
        });

        var quantity = OffQuantityParser.Parse(product.Quantity, product.QuantityUnit);
        var suggestion = new ProductSuggestionDto(
            barcode,
            product.Name,
            // « Nutella, Ferrero » : la première marque est la plus parlante.
            product.Brands?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
            OffCategoryResolver.Resolve(product.CategoryTags, rules!),
            quantity?.Quantity,
            quantity?.Unit,
            product.ImageUrl,
            SourceName);

        cache.Set(cacheKey, suggestion, FoundCacheDuration);
        return suggestion;
    }
}
