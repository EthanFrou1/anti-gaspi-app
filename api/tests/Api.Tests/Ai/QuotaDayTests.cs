using Api.Services.Ai;

namespace Api.Tests.Ai;

public class QuotaDayTests
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    [Fact]
    public void SummerDay_RunsFromMidnightToMidnightParisTime()
    {
        // 23 septembre 2026, 14 h UTC = 16 h à Paris (UTC+2).
        var (start, end, date) = QuotaDay.Window(new DateTimeOffset(2026, 9, 23, 14, 0, 0, TimeSpan.Zero), Paris);

        Assert.Equal(new DateOnly(2026, 9, 23), date);
        Assert.Equal(new DateTimeOffset(2026, 9, 22, 22, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 9, 23, 22, 0, 0, TimeSpan.Zero), end);
    }

    [Fact]
    public void LateEveningUtc_IsAlreadyTomorrowInParis()
    {
        // 22 h 30 UTC = 0 h 30 le lendemain à Paris : nouveau jour, nouveaux quotas.
        var (_, _, date) = QuotaDay.Window(new DateTimeOffset(2026, 9, 23, 22, 30, 0, TimeSpan.Zero), Paris);

        Assert.Equal(new DateOnly(2026, 9, 24), date);
    }

    [Theory]
    [InlineData(2026, 3, 29, 23)] // passage à l'heure d'été : journée de 23 h
    [InlineData(2026, 10, 25, 25)] // passage à l'heure d'hiver : journée de 25 h
    [InlineData(2026, 9, 23, 24)]
    public void DaylightSavingDays_HaveTheirRealLength(int year, int month, int day, int expectedHours)
    {
        var noonUtc = new DateTimeOffset(year, month, day, 10, 0, 0, TimeSpan.Zero);

        var (start, end, _) = QuotaDay.Window(noonUtc, Paris);

        Assert.Equal(TimeSpan.FromHours(expectedHours), end - start);
    }
}
