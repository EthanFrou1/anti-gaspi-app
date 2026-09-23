using Api.Authorization;
using Api.Common;
using Api.Dtos.Households;
using Api.Services.Households;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

[Route("api/households")]
public sealed class HouseholdsController(IHouseholdService householdService) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<HouseholdDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HouseholdDto>> Create(CreateHouseholdRequest request, CancellationToken ct)
    {
        var result = await householdService.CreateAsync(User.GetUserId(), request, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetMine), result.Value) : ToProblem(result.Error);
    }

    [HttpGet("mine")]
    [ProducesResponseType<HouseholdDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HouseholdDto>> GetMine(CancellationToken ct)
    {
        var result = await householdService.GetMineAsync(User.GetUserId(), ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }

    [HttpPost("join")]
    [EnableRateLimiting(RateLimitPolicies.JoinHousehold)]
    [ProducesResponseType<HouseholdDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HouseholdDto>> Join(JoinHouseholdRequest request, CancellationToken ct)
    {
        var result = await householdService.JoinAsync(User.GetUserId(), request.Code, ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }

    // Équipement de la cuisine commune : modifiable par tout membre du foyer.
    [HttpPut("{householdId:guid}/equipment")]
    [Authorize(Policy = HouseholdPolicies.Member)]
    [ProducesResponseType<HouseholdDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HouseholdDto>> UpdateEquipment(
        Guid householdId, UpdateEquipmentRequest request, CancellationToken ct)
    {
        var result = await householdService.UpdateEquipmentAsync(User.GetUserId(), householdId, request.Equipment, ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }

    // Quitter le foyer (userId = soi-même) ou retirer un membre (propriétaire uniquement,
    // vérifié par le service car la règle dépend de la personne ciblée).
    [HttpDelete("{householdId:guid}/members/{userId:guid}")]
    [Authorize(Policy = HouseholdPolicies.Member)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid householdId, Guid userId, CancellationToken ct)
    {
        var result = await householdService.RemoveMemberAsync(User.GetUserId(), householdId, userId, ct);
        return result.IsSuccess ? NoContent() : ToProblem(result.Error);
    }
}
