using Api.Entities;

namespace Api.Services.Products;

/// <summary>
/// Convertit la quantité d'Open Food Facts (ex. 400 + « g ») vers les unités de l'app.
/// Fonction pure et testable.
/// </summary>
public static class OffQuantityParser
{
    public static (decimal Quantity, QuantityUnit Unit)? Parse(decimal? quantity, string? unit)
    {
        if (quantity is not > 0 || string.IsNullOrWhiteSpace(unit))
        {
            return null;
        }

        return unit.Trim().ToLowerInvariant() switch
        {
            "g" => (quantity.Value, QuantityUnit.Gram),
            "kg" => (quantity.Value, QuantityUnit.Kilogram),
            "ml" => (quantity.Value, QuantityUnit.Milliliter),
            // Les centilitres n'existent pas dans l'app : 50 cl = 500 ml.
            "cl" => (quantity.Value * 10, QuantityUnit.Milliliter),
            "l" => (quantity.Value, QuantityUnit.Liter),
            // Unité inconnue (« oz », « pièces »…) : l'utilisateur saisira lui-même.
            _ => null,
        };
    }
}
