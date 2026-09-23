using Api.Entities;
using Api.Services.Products;

namespace Api.Tests.Products;

public class OffQuantityParserTests
{
    [Theory]
    [InlineData(400, "g", 400, QuantityUnit.Gram)]
    [InlineData(1, "kg", 1, QuantityUnit.Kilogram)]
    [InlineData(250, "ml", 250, QuantityUnit.Milliliter)]
    [InlineData(50, "cl", 500, QuantityUnit.Milliliter)]   // centilitres convertis
    [InlineData(1.5, "l", 1.5, QuantityUnit.Liter)]
    [InlineData(400, " G ", 400, QuantityUnit.Gram)]       // casse et espaces tolérés
    public void Parse_ConvertsKnownUnits(double quantity, string unit, double expectedQuantity, QuantityUnit expectedUnit)
    {
        var result = OffQuantityParser.Parse((decimal)quantity, unit);

        Assert.Equal(((decimal)expectedQuantity, expectedUnit), result);
    }

    [Theory]
    [InlineData(12.0, "oz")]
    [InlineData(6.0, "pièces")]
    [InlineData(0.0, "g")]
    [InlineData(-1.0, "g")]
    [InlineData(null, "g")]
    [InlineData(400.0, null)]
    [InlineData(400.0, "")]
    public void Parse_ReturnsNullWhenUnusable(double? quantity, string? unit)
    {
        Assert.Null(OffQuantityParser.Parse((decimal?)quantity, unit));
    }
}
