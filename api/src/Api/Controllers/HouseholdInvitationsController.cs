using Api.Authorization;
using Api.Common;
using Api.Dtos.Households;
using Api.Services.Households;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

// Tout membre du foyer présent dans l'URL peut inviter. Les règles de révocation
// (auteur de l'invitation ou propriétaire) sont appliquées par le service.
[Route("api/households/{householdId:guid}/invitations")]
[Authorize(Policy = HouseholdPolicies.Member)]
public sealed class HouseholdInvitationsController(IInvitationService invitationService) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<InvitationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvitationDto>> Create(Guid householdId, CancellationToken ct)
    {
        var result = await invitationService.CreateAsync(User.GetUserId(), householdId, ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InvitationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InvitationDto>>> ListActive(Guid householdId, CancellationToken ct) =>
        Ok(await invitationService.ListActiveAsync(householdId, ct));

    [HttpDelete("{invitationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid householdId, Guid invitationId, CancellationToken ct)
    {
        var result = await invitationService.RevokeAsync(User.GetUserId(), householdId, invitationId, ct);
        return result.IsSuccess ? NoContent() : ToProblem(result.Error);
    }
}
