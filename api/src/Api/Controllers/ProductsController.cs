using Api.Common;
using Api.Dtos.Products;
using Api.Services.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

// Il suffit d'être connecté (FallbackPolicy) : la recherche ne touche aucune donnée du foyer.
[Route("api/products")]
public sealed class ProductsController(IProductLookupService lookupService) : ApiControllerBase
{
    /// <summary>
    /// Suggestion de pré-remplissage à partir d'un code-barres (EAN-8, EAN-13, UPC, GTIN-14).
    /// </summary>
    // La contrainte de route n'accepte que 8 à 14 chiffres : tout autre texte donne un 404
    // sans jamais atteindre Open Food Facts.
    [HttpGet("barcode/{barcode:regex(^\\d{{8,14}}$)}")]
    [EnableRateLimiting(RateLimitPolicies.ProductLookup)]
    [ProducesResponseType<ProductSuggestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ProductSuggestionDto>> GetByBarcode(string barcode, CancellationToken ct)
    {
        var result = await lookupService.LookupAsync(barcode, ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }
}
