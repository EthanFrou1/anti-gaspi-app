using Api.Authorization;
using Api.Common;
using Api.Dtos.Inventory;
using Api.Entities;
using Api.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

// Réservé aux membres du foyer présent dans l'URL. Les droits sur les produits perso
// (seul le propriétaire les modifie) sont appliqués par le service.
[Route("api/households/{householdId:guid}/items")]
[Authorize(Policy = HouseholdPolicies.Member)]
public sealed class InventoryItemsController(IInventoryService inventoryService) : ApiControllerBase
{
    /// <summary>
    /// Produits du foyer triés par date de péremption (actifs par défaut).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InventoryItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InventoryItemDto>>> List(
        Guid householdId, [FromQuery] InventoryItemStatus status = InventoryItemStatus.Active, CancellationToken ct = default) =>
        Ok(await inventoryService.ListAsync(householdId, status, ct));

    [HttpGet("{itemId:guid}")]
    [ProducesResponseType<InventoryItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryItemDto>> Get(Guid householdId, Guid itemId, CancellationToken ct)
    {
        var result = await inventoryService.GetAsync(householdId, itemId, ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }

    [HttpPost]
    [ProducesResponseType<InventoryItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryItemDto>> Create(
        Guid householdId, SaveInventoryItemRequest request, CancellationToken ct)
    {
        var result = await inventoryService.CreateAsync(User.GetUserId(), householdId, request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { householdId, itemId = result.Value.Id }, result.Value)
            : ToProblem(result.Error);
    }

    [HttpPut("{itemId:guid}")]
    [ProducesResponseType<InventoryItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryItemDto>> Update(
        Guid householdId, Guid itemId, SaveInventoryItemRequest request, CancellationToken ct)
    {
        var result = await inventoryService.UpdateAsync(User.GetUserId(), householdId, itemId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }

    [HttpPost("{itemId:guid}/consume")]
    [ProducesResponseType<InventoryItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<InventoryItemDto>> Consume(Guid householdId, Guid itemId, CancellationToken ct) =>
        ChangeStatus(householdId, itemId, InventoryItemStatus.Consumed, ct);

    [HttpPost("{itemId:guid}/discard")]
    [ProducesResponseType<InventoryItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<InventoryItemDto>> Discard(Guid householdId, Guid itemId, CancellationToken ct) =>
        ChangeStatus(householdId, itemId, InventoryItemStatus.Discarded, ct);

    // Réservé à la correction d'une erreur de saisie (sinon : consommé ou jeté).
    [HttpDelete("{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid householdId, Guid itemId, CancellationToken ct)
    {
        var result = await inventoryService.DeleteAsync(User.GetUserId(), householdId, itemId, ct);
        return result.IsSuccess ? NoContent() : ToProblem(result.Error);
    }

    private async Task<ActionResult<InventoryItemDto>> ChangeStatus(
        Guid householdId, Guid itemId, InventoryItemStatus status, CancellationToken ct)
    {
        var result = await inventoryService.ChangeStatusAsync(User.GetUserId(), householdId, itemId, status, ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }
}
