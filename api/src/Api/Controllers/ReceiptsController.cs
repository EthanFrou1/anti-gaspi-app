using Api.Authorization;
using Api.Common;
using Api.Dtos.Receipts;
using Api.Services.Receipts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

// Réservé aux membres du foyer présent dans l'URL : les produits lus iront dans son frigo.
[Route("api/households/{householdId:guid}/receipts")]
[Authorize(Policy = HouseholdPolicies.Member)]
public sealed class ReceiptsController(IReceiptService receiptService) : ApiControllerBase
{
    // Image de 2 Mo au plus, plus l'enveloppe multipart (en-têtes, séparateurs).
    private const int MaxRequestBytes = ReceiptImageFormat.MaxBytes + 64 * 1024;

    /// <summary>
    /// Lit la photo d'un ticket de caisse (champ « image », JPEG, 2 Mo au plus) et
    /// renvoie les produits reconnus, à valider par l'utilisateur. Rien n'est ajouté au frigo,
    /// et l'image n'est jamais enregistrée. Quota : 3 par jour et par utilisateur.
    /// </summary>
    [HttpPost("scan")]
    [Consumes("multipart/form-data")]
    // Limites appliquées pendant la réception : un envoi trop gros est coupé avant d'être lu en entier.
    // MemoryBufferThreshold : par défaut, ASP.NET Core écrit tout fichier de plus de 64 Ko dans un
    // fichier temporaire sur disque. Ici, la photo reste en mémoire jusqu'à la taille maximale
    // (au-delà, l'envoi est refusé avant d'avoir pu déborder sur le disque).
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes, MemoryBufferThreshold = MaxRequestBytes)]
    [ProducesResponseType<ReceiptScanDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ReceiptScanDto>> Scan(Guid householdId, IFormFile? image, CancellationToken ct)
    {
        if (image is null || image.Length is 0 or > ReceiptImageFormat.MaxBytes)
        {
            return ToProblem(ReceiptErrors.InvalidImage);
        }

        // En mémoire seulement (jamais sur disque) : l'image disparaît avec la requête.
        using var buffer = new MemoryStream((int)image.Length);
        await image.CopyToAsync(buffer, ct);

        var result = await receiptService.ScanAsync(User.GetUserId(), buffer.ToArray(), ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }
}
