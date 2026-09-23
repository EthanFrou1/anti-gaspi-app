using System.Security.Claims;
using Api.Authorization;
using Api.Services.Households;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Api.Tests.Households;

/// <summary>
/// Vérifie la porte d'entrée de toutes les routes /api/households/{householdId}/… :
/// l'appartenance au foyer et, si demandé, le rôle de propriétaire.
/// </summary>
public class HouseholdAuthorizationHandlerTests(DatabaseFixture database) : HouseholdTestBase(database)
{
    [Fact]
    public async Task Member_IsAllowedOnMemberPolicy()
    {
        var (_, bob, householdId) = await CreateHouseholdWithMemberAsync();

        var context = await AuthorizeAsync(bob, householdId.ToString(), ownerOnly: false);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task NonMember_IsRejectedAsNotMember()
    {
        var (_, _, householdId) = await CreateHouseholdWithMemberAsync();
        var mallory = await CreateUserAsync("Mallory");

        var context = await AuthorizeAsync(mallory, householdId.ToString(), ownerOnly: false);

        Assert.False(context.HasSucceeded);
        Assert.Contains(context.FailureReasons, r => r.Message == HouseholdAuthorizationHandler.NotMemberReason);
    }

    [Fact]
    public async Task OwnerOfAnotherHousehold_IsRejectedAsNotMember()
    {
        var (_, _, householdId) = await CreateHouseholdWithMemberAsync();
        var mallory = await CreateUserAsync("Mallory");
        await CreateHouseholdAsync(mallory, "Chez Mallory");

        var context = await AuthorizeAsync(mallory, householdId.ToString(), ownerOnly: true);

        Assert.False(context.HasSucceeded);
        Assert.Contains(context.FailureReasons, r => r.Message == HouseholdAuthorizationHandler.NotMemberReason);
    }

    [Fact]
    public async Task Member_IsRejectedOnOwnerPolicy()
    {
        var (_, bob, householdId) = await CreateHouseholdWithMemberAsync();

        var context = await AuthorizeAsync(bob, householdId.ToString(), ownerOnly: true);

        Assert.False(context.HasSucceeded);
        Assert.Contains(context.FailureReasons, r => r.Message == HouseholdAuthorizationHandler.NotOwnerReason);
    }

    [Fact]
    public async Task Owner_IsAllowedOnOwnerPolicy()
    {
        var (owner, _, householdId) = await CreateHouseholdWithMemberAsync();

        var context = await AuthorizeAsync(owner, householdId.ToString(), ownerOnly: true);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task InvalidHouseholdIdInRoute_IsRejected()
    {
        var (owner, _, _) = await CreateHouseholdWithMemberAsync();

        var context = await AuthorizeAsync(owner, "pas-un-guid", ownerOnly: false);

        Assert.False(context.HasSucceeded);
    }

    private async Task<(Guid Owner, Guid Member, Guid HouseholdId)> CreateHouseholdWithMemberAsync()
    {
        var owner = await CreateUserAsync("Owner");
        var bob = await CreateUserAsync("Bob");
        var householdId = await CreateHouseholdAsync(owner);
        await AddMemberAsync(owner, householdId, bob);
        return (owner, bob, householdId);
    }

    private async Task<AuthorizationHandlerContext> AuthorizeAsync(Guid userId, string routeHouseholdId, bool ownerOnly)
    {
        await using var scope = CreateScope();
        var handler = new HouseholdAuthorizationHandler(
            scope.ServiceProvider.GetRequiredService<IHouseholdAccessService>());

        // Simule une requête HTTP authentifiée dont la route contient {householdId}.
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())], authenticationType: "Test"));
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues[HouseholdPolicies.RouteKey] = routeHouseholdId;

        var context = new AuthorizationHandlerContext(
            [new HouseholdAccessRequirement(ownerOnly)], user, httpContext);
        await handler.HandleAsync(context);
        return context;
    }
}
