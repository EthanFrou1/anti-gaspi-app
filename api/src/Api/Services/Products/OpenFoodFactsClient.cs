using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Api.Services.Products;

/// <summary>
/// Produit tel que renvoyé par Open Food Facts (seuls les champs utiles).
/// </summary>
public sealed record OffProduct(
    string Barcode,
    string? Name,
    string? Brands,
    IReadOnlyList<string> CategoryTags,
    decimal? Quantity,
    string? QuantityUnit,
    string? ImageUrl);

/// <summary>
/// Open Food Facts ne répond pas (panne, délai dépassé, quota atteint…).
/// </summary>
public sealed class OpenFoodFactsUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);

public interface IOpenFoodFactsClient
{
    /// <summary>
    /// Renvoie le produit, ou null s'il n'est pas référencé.
    /// Lève <see cref="OpenFoodFactsUnavailableException"/> si OFF est injoignable.
    /// </summary>
    Task<OffProduct?> GetProductAsync(string barcode, CancellationToken ct);
}

/// <summary>
/// Client HTTP typé : son HttpClient (adresse, délai, User-Agent) est configuré
/// à l'enregistrement, dans ServiceCollectionExtensions.
/// </summary>
public sealed class OpenFoodFactsClient(
    HttpClient http,
    OpenFoodFactsRateLimiter rateLimiter,
    ILogger<OpenFoodFactsClient> logger) : IOpenFoodFactsClient
{
    // On ne demande que les champs utiles : réponse plus légère et plus rapide.
    private const string Fields =
        "code,product_name,product_name_fr,generic_name_fr,brands,categories_tags," +
        "product_quantity,product_quantity_unit,image_front_small_url";

    public async Task<OffProduct?> GetProductAsync(string barcode, CancellationToken ct)
    {
        // Limite globale atteinte : on n'appelle pas OFF (on risquerait un blocage de l'IP
        // du serveur) et l'app bascule en saisie manuelle.
        if (!rateLimiter.TryAcquire())
        {
            logger.LogWarning("Limite globale d'appels à Open Food Facts atteinte : recherche du code {Barcode} refusée.", barcode);
            throw new OpenFoodFactsUnavailableException("Limite d'appels à Open Food Facts atteinte.");
        }

        // Le code-barres ne contient que des chiffres (validé par la route) : aucun risque
        // d'injecter autre chose dans l'URL appelée.
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync($"api/v2/product/{barcode}?fields={Fields}", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // TaskCanceledException sans annulation demandée = délai dépassé.
            logger.LogWarning(ex, "Open Food Facts injoignable pour le code {Barcode}.", barcode);
            throw new OpenFoodFactsUnavailableException("Open Food Facts est injoignable.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Open Food Facts a répondu {StatusCode} pour le code {Barcode}.", (int)response.StatusCode, barcode);
                throw new OpenFoodFactsUnavailableException($"Open Food Facts a répondu {(int)response.StatusCode}.");
            }

            OffResponse? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<OffResponse>(ct);
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new OpenFoodFactsUnavailableException("Réponse d'Open Food Facts illisible.", ex);
            }

            // OFF répond 200 avec « status: 0 » pour un code inconnu.
            if (body?.Status != 1 || body.Product is null)
            {
                return null;
            }

            var p = body.Product;
            return new OffProduct(
                barcode,
                FirstNonEmpty(p.ProductNameFr, p.ProductName, p.GenericNameFr),
                FirstNonEmpty(p.Brands),
                p.CategoriesTags ?? [],
                p.ProductQuantity,
                FirstNonEmpty(p.ProductQuantityUnit),
                FirstNonEmpty(p.ImageFrontSmallUrl));
        }
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrEmpty(v));

    // Format de la réponse OFF (API v2).
    private sealed record OffResponse(
        [property: JsonPropertyName("status")] int Status,
        [property: JsonPropertyName("product")] OffResponseProduct? Product);

    private sealed record OffResponseProduct(
        [property: JsonPropertyName("product_name")] string? ProductName,
        [property: JsonPropertyName("product_name_fr")] string? ProductNameFr,
        [property: JsonPropertyName("generic_name_fr")] string? GenericNameFr,
        [property: JsonPropertyName("brands")] string? Brands,
        [property: JsonPropertyName("categories_tags")] List<string>? CategoriesTags,
        // OFF renvoie parfois la quantité en texte (« 400 ») : on accepte les deux formats.
        [property: JsonPropertyName("product_quantity"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        decimal? ProductQuantity,
        [property: JsonPropertyName("product_quantity_unit")] string? ProductQuantityUnit,
        [property: JsonPropertyName("image_front_small_url")] string? ImageFrontSmallUrl);
}
