using System.Reflection;
using Api.Authorization;
using Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Api.Tests.Security;

/// <summary>
/// Règle de sécurité n° 3 du projet, vérifiée automatiquement : « chaque endpoint vérifie
/// que l'utilisateur appartient bien au foyer dont il manipule les données ».
///
/// Le test parcourt tous les contrôleurs par réflexion. Toute route qui contient
/// {householdId} doit être protégée par une politique de foyer. Si un futur endpoint
/// l'oublie, ce test échoue avant que le code n'arrive en production.
/// </summary>
public class EndpointAuthorizationTests
{
    private static readonly string[] HouseholdPolicyNames = [HouseholdPolicies.Member, HouseholdPolicies.Owner];

    public static TheoryData<string> HouseholdRoutes()
    {
        var data = new TheoryData<string>();
        foreach (var endpoint in HouseholdEndpoints())
        {
            data.Add(endpoint.Name);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(HouseholdRoutes))]
    public void HouseholdRoute_IsProtectedByAHouseholdPolicy(string endpointName)
    {
        var endpoint = HouseholdEndpoints().Single(e => e.Name == endpointName);

        var attributes = endpoint.Controller.GetCustomAttributes(inherit: true)
            .Concat(endpoint.Action.GetCustomAttributes(inherit: true))
            .ToList();

        Assert.DoesNotContain(attributes, a => a is AllowAnonymousAttribute);
        Assert.Contains(attributes.OfType<AuthorizeAttribute>(), a => HouseholdPolicyNames.Contains(a.Policy));
    }

    [Fact]
    public void Discovery_FindsTheKnownHouseholdRoutes()
    {
        // Garde-fou : si la découverte des routes cassait, le test ci-dessus ne vérifierait plus rien.
        var names = HouseholdEndpoints().Select(e => e.Name).ToList();

        Assert.Contains(names, n => n.StartsWith("InventoryItemsController.", StringComparison.Ordinal));
        Assert.Contains(names, n => n.StartsWith("HouseholdInvitationsController.", StringComparison.Ordinal));
        Assert.Contains("HouseholdsController.RemoveMember", names);
    }

    private sealed record Endpoint(string Name, Type Controller, MethodInfo Action);

    private static IEnumerable<Endpoint> HouseholdEndpoints()
    {
        var controllers = typeof(ApiControllerBase).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var controller in controllers)
        {
            var controllerRoute = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;

            var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

            foreach (var action in actions)
            {
                var actionRoutes = action.GetCustomAttributes<HttpMethodAttribute>().Select(a => a.Template ?? string.Empty);
                if (actionRoutes.Any(r => $"{controllerRoute}/{r}".Contains("{" + HouseholdPolicies.RouteKey, StringComparison.Ordinal)))
                {
                    yield return new Endpoint($"{controller.Name}.{action.Name}", controller, action);
                }
            }
        }
    }
}
