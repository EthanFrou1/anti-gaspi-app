using Api.Common;
using Api.Dtos.Receipts;
using Api.Services.Receipts;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/me/receipt-quota")]
public sealed class ReceiptQuotaController(IReceiptService receiptService) : ApiControllerBase
{
    /// <summary>Scans de ticket restants aujourd'hui (remise à zéro à minuit, heure de Paris).</summary>
    [HttpGet]
    [ProducesResponseType<ReceiptQuotaDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReceiptQuotaDto>> Get(CancellationToken ct) =>
        Ok(await receiptService.GetQuotaAsync(User.GetUserId(), ct));
}
