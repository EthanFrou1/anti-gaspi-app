using Api.Services.Inventory;

namespace Api.Tests.Inventory;

public class ExpiryEstimatorTests
{
    [Theory]
    [InlineData("2026-09-23", 3, "2026-09-26")]   // cas simple
    [InlineData("2026-09-29", 3, "2026-10-02")]   // changement de mois
    [InlineData("2026-12-30", 7, "2027-01-06")]   // changement d'année
    [InlineData("2028-02-27", 3, "2028-03-01")]   // année bissextile : passe par le 29 février
    [InlineData("2027-02-27", 3, "2027-03-02")]   // année non bissextile
    [InlineData("2026-10-24", 2, "2026-10-26")]   // passage à l'heure d'hiver : sans effet sur une date
    public void Estimate_AddsShelfLifeToPurchaseDate(string purchasedOn, int shelfLifeDays, string expected)
    {
        var result = ExpiryEstimator.Estimate(DateOnly.Parse(purchasedOn), shelfLifeDays);

        Assert.Equal(DateOnly.Parse(expected), result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Estimate_RejectsNonPositiveShelfLife(int shelfLifeDays)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExpiryEstimator.Estimate(new DateOnly(2026, 9, 23), shelfLifeDays));
    }
}
