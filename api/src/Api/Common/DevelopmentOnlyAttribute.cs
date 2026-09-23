using System.Reflection;
using Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Api.Common;

/// <summary>
/// Marque une action réservée à l'environnement Development (outils de mise au point).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DevelopmentOnlyAttribute : Attribute;

/// <summary>
/// Hors Development, les actions [DevelopmentOnly] sont RETIRÉES au démarrage : elles
/// n'existent pas, pour personne (pas de « 401 » ou « 403 » qui révélerait leur présence).
/// </summary>
public sealed class RemoveDevelopmentOnlyActionsConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            var devOnly = controller.Actions
                .Where(a => a.ActionMethod.IsDefined(typeof(DevelopmentOnlyAttribute), inherit: true))
                .ToList();
            foreach (var action in devOnly)
            {
                controller.Actions.Remove(action);
            }
        }
    }
}

public static class DevelopmentOnlyEndpoints
{
    /// <summary>
    /// Complète le retrait des actions : pour chaque chemin [DevelopmentOnly], une route qui
    /// répond 404 à toutes les méthodes. Sans elle, le routage peut répondre 405 : par exemple,
    /// « recipes/prompt-preview » a la même forme que « GET recipes/{recipeId} », et le routeur
    /// signale une méthode non autorisée avant de vérifier que « prompt-preview » n'est pas un GUID.
    /// </summary>
    public static void MapNotFoundStubs(IEndpointRouteBuilder app)
    {
        foreach (var template in Templates())
        {
            app.Map(template, () => Results.NotFound())
                .AllowAnonymous()
                .ExcludeFromDescription();
        }
    }

    // Chemins des actions [DevelopmentOnly], reconstitués depuis les attributs de routage.
    public static IEnumerable<string> Templates() =>
        typeof(ApiControllerBase).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(controller => controller.GetMethods()
                .Where(m => m.IsDefined(typeof(DevelopmentOnlyAttribute)))
                .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>()
                    .Select(http => Combine(controller.GetCustomAttribute<RouteAttribute>()?.Template, http.Template))))
            .Distinct();

    private static string Combine(string? controllerTemplate, string? actionTemplate) =>
        string.Join('/', new[] { controllerTemplate, actionTemplate }.Where(p => !string.IsNullOrEmpty(p)));
}
