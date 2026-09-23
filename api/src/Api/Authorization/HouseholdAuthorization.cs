using Api.Common;
using Api.Entities;
using Api.Services.Households;
using Microsoft.AspNetCore.Authorization;

namespace Api.Authorization;

public static class HouseholdPolicies
{
    public const string Member = "HouseholdMember";
    public const string Owner = "HouseholdOwner";

    // Nom du paramètre de route qui porte l'identifiant du foyer : les routes protégées
    // doivent l'utiliser, ex. [Route("api/households/{householdId:guid}/...")].
    public const string RouteKey = "householdId";
}

/// <summary>
/// Exigence « être membre (ou propriétaire) du foyer ciblé par la route ».
/// </summary>
public sealed class HouseholdAccessRequirement(bool ownerOnly) : IAuthorizationRequirement
{
    public bool OwnerOnly { get; } = ownerOnly;
}

/// <summary>
/// Vérifie l'appartenance au foyer AVANT que le contrôleur ne s'exécute.
/// Un endpoint protégé par la politique ne peut donc pas oublier la vérification.
/// </summary>
public sealed class HouseholdAuthorizationHandler(IHouseholdAccessService access)
    : AuthorizationHandler<HouseholdAccessRequirement>
{
    public const string NotMemberReason = "household.not_member";
    public const string NotOwnerReason = "household.not_owner";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, HouseholdAccessRequirement requirement)
    {
        // Utilisateur non connecté : l'exigence RequireAuthenticatedUser de la politique
        // produit déjà un 401, rien à ajouter ici.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (context.Resource is not HttpContext httpContext)
        {
            throw new InvalidOperationException("La politique de foyer ne s'applique qu'aux requêtes HTTP.");
        }

        var routeValue = httpContext.GetRouteValue(HouseholdPolicies.RouteKey)?.ToString()
            ?? throw new InvalidOperationException(
                $"Route sans paramètre '{HouseholdPolicies.RouteKey}' protégée par une politique de foyer.");

        if (!Guid.TryParse(routeValue, out var householdId))
        {
            context.Fail(new AuthorizationFailureReason(this, NotMemberReason));
            return;
        }

        var role = await access.GetRoleAsync(context.User.GetUserId(), householdId, httpContext.RequestAborted);

        if (role is null)
        {
            context.Fail(new AuthorizationFailureReason(this, NotMemberReason));
        }
        else if (requirement.OwnerOnly && role != HouseholdRole.Owner)
        {
            context.Fail(new AuthorizationFailureReason(this, NotOwnerReason));
        }
        else
        {
            context.Succeed(requirement);
        }
    }
}
