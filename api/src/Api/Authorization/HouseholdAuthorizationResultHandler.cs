using Api.Common;
using Api.Services.Households;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace Api.Authorization;

/// <summary>
/// Personnalise la réponse quand l'accès à un foyer est refusé :
/// - non-membre : 404, pour ne pas révéler qu'un foyer existe à cette adresse ;
/// - membre mais pas propriétaire : 403 (il sait déjà que le foyer existe).
/// Les autres refus gardent le comportement standard d'ASP.NET Core.
/// </summary>
public sealed class HouseholdAuthorizationResultHandler(IProblemDetailsService problemDetails)
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var reasons = authorizeResult.AuthorizationFailure?.FailureReasons ?? [];
        var error = authorizeResult.Forbidden switch
        {
            true when reasons.Any(r => r.Message == HouseholdAuthorizationHandler.NotMemberReason) => HouseholdErrors.NotFound,
            true when reasons.Any(r => r.Message == HouseholdAuthorizationHandler.NotOwnerReason) => HouseholdErrors.OwnerOnly,
            _ => null,
        };

        if (error is null)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var status = error.Type == ErrorType.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status403Forbidden;
        context.Response.StatusCode = status;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = error.Message,
                Extensions = { ["code"] = error.Code },
            },
        });
    }
}
