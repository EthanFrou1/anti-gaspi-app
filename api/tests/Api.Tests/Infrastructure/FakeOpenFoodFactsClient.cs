using System.Collections.Concurrent;
using Api.Services.Products;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Faux Open Food Facts : les tests décident de la réponse pour chaque code-barres,
/// sans jamais dépendre d'internet ni de la disponibilité d'OFF.
/// </summary>
public sealed class FakeOpenFoodFactsClient : IOpenFoodFactsClient
{
    private readonly ConcurrentDictionary<string, OffProduct> _products = new();
    private readonly ConcurrentDictionary<string, bool> _unavailable = new();
    private int _calls;

    public int Calls => _calls;

    public void Add(OffProduct product) => _products[product.Barcode] = product;

    public void MakeUnavailable(string barcode) => _unavailable[barcode] = true;

    public void MakeAvailable(string barcode) => _unavailable.TryRemove(barcode, out _);

    public Task<OffProduct?> GetProductAsync(string barcode, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);

        if (_unavailable.ContainsKey(barcode))
        {
            throw new OpenFoodFactsUnavailableException("Panne simulée.");
        }

        return Task.FromResult(_products.TryGetValue(barcode, out var product) ? product : null);
    }

    // Exemple réel (skyr Danone), catégories reprises telles que renvoyées par OFF.
    public static OffProduct Skyr(string barcode = "3033490004743") => new(
        barcode,
        "Skyr",
        "DANONE PRODUITS FRAIS FRANCE",
        ["en:dairies", "en:fermented-foods", "en:desserts", "en:fermented-milk-products", "en:cheeses",
            "en:dairy-desserts", "en:fermented-dairy-desserts", "en:yogurts", "en:plain-skyrs"],
        140,
        "g",
        "https://images.openfoodfacts.org/images/products/303/349/000/4743/front_fr.148.200.jpg");
}
