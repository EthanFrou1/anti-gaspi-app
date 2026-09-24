using System.Text.RegularExpressions;

namespace Api.Services.Products;

/// <summary>
/// User-Agent demandé par Open Food Facts : « NomApp/Version (contact) ».
/// Il permet à OFF d'identifier l'application et de la contacter en cas de souci.
/// </summary>
public static partial class OpenFoodFactsUserAgent
{
    public const string Version = "1.0";

    public static string Build(string appName, string? contactEmail)
    {
        // Le nom de produit d'un User-Agent ne peut contenir ni espace ni tiret spécial :
        // « Mon-Frigo » deviendrait « MonFrigo ».
        var product = NonAlphanumeric().Replace(appName, string.Empty);
        if (product.Length == 0)
        {
            product = "App";
        }

        return string.IsNullOrWhiteSpace(contactEmail)
            ? $"{product}/{Version}"
            : $"{product}/{Version} ({contactEmail.Trim()})";
    }

    [GeneratedRegex("[^A-Za-z0-9]")]
    private static partial Regex NonAlphanumeric();
}
