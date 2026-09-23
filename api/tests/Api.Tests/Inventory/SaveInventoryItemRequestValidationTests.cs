using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using Api.Dtos.Inventory;

namespace Api.Tests.Inventory;

/// <summary>
/// Régression : sur une machine en français, [Range(typeof(decimal), "0.001", ...)] ne savait
/// pas lire ses bornes (séparateur décimal = virgule) et toute création de produit
/// renvoyait une erreur 500.
/// </summary>
public class SaveInventoryItemRequestValidationTests
{
    // Sur un record positionnel, ASP.NET lit les attributs de validation sur les paramètres du constructeur.
    private static RangeAttribute QuantityRange() =>
        typeof(SaveInventoryItemRequest).GetConstructors().Single()
            .GetParameters().Single(p => p.Name == nameof(SaveInventoryItemRequest.Quantity))
            .GetCustomAttribute<RangeAttribute>()!;

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    public void QuantityRange_WorksWhateverTheServerCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        try
        {
            var range = QuantityRange();

            Assert.True(range.IsValid(0.5m));
            Assert.True(range.IsValid(1000m));
            Assert.False(range.IsValid(0m));
            Assert.False(range.IsValid(-1m));
            Assert.False(range.IsValid(1_000_000m));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
