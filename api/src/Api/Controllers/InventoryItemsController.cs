using Api.Authorization;
using Api.Common;
using Api.Dtos.Inventory;
using Api.Entities;
using Api.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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

    /// <summary>
    /// Ajout groupé (validation d'un ticket de caisse) : tous les produits, ou aucun.
    /// </summary>
    [HttpPost("batch")]
    [ProducesResponseType<IReadOnlyList<InventoryItemDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyList<InventoryItemDto>>> CreateMany(
        Guid householdId, CreateInventoryItemsRequest request, CancellationToken ct)
    {
        var result = await inventoryService.CreateManyAsync(User.GetUserId(), householdId, request.Items, ct);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : ToProblem(result.Error);
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

    /// <summary>
    /// Produit consommé, en entier (sans corps) ou en partie (<c>{ "quantity": 200 }</c>).
    /// </summary>
    [HttpPost("{itemId:guid}/consume")]
    [ProducesResponseType<InventoryItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<InventoryItemDto>> Consume(
        Guid householdId, Guid itemId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ChangeStatusRequest? request, CancellationToken ct) =>
        ChangeStatus(householdId, itemId, InventoryItemStatus.Consumed, request?.Quantity, ct);

    /// <summary>
    /// Produit jeté, en entier (sans corps) ou en partie (<c>{ "quantity": 200 }</c>).
    /// </summary>
    [HttpPost("{itemId:guid}/discard")]
    [ProducesResponseType<InventoryItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<InventoryItemDto>> Discard(
        Guid householdId, Guid itemId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ChangeStatusRequest? request, CancellationToken ct) =>
        ChangeStatus(householdId, itemId, InventoryItemStatus.Discarded, request?.Quantity, ct);

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
        Guid householdId, Guid itemId, InventoryItemStatus status, decimal? quantity, CancellationToken ct)
    {
        var result = await inventoryService.ChangeStatusAsync(User.GetUserId(), householdId, itemId, status, quantity, ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }
}
