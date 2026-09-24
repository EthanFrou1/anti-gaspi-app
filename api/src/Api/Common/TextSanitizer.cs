using System.Text.RegularExpressions;

namespace Api.Common;

/// <summary>
/// Nettoyage d'un texte libre destiné à l'IA ou venant d'elle (noms de produits) :
/// caractères de contrôle et retours à la ligne retirés, espaces normalisés, longueur bornée.
/// </summary>
public static partial class TextSanitizer
{
    public static string SingleLine(string? value, int maxLength)
    {
        var cleaned = ControlCharacters().Replace(value ?? string.Empty, " ");
        cleaned = MultipleSpaces().Replace(cleaned, " ").Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength].TrimEnd();
    }

    [GeneratedRegex(@"[\p{C}]")]
    private static partial Regex ControlCharacters();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpaces();
}
