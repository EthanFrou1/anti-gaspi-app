namespace Api.Services.Ai;

/// <summary>
/// « Aujourd'hui » au sens des quotas : de minuit à minuit dans le fuseau configuré
/// (Europe/Paris), converti en instants UTC pour interroger la base. Fonction pure.
/// </summary>
public static class QuotaDay
{
    public static (DateTimeOffset StartUtc, DateTimeOffset EndUtc, DateOnly LocalDate) Window(DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var date = DateOnly.FromDateTime(local.DateTime);
        return (ToUtc(date, zone), ToUtc(date.AddDays(1), zone), date);
    }

    // Minuit local → instant UTC. Les jours de changement d'heure durent 23 h ou 25 h :
    // on calcule le décalage propre à CE minuit, pas celui de l'instant courant.
    private static DateTimeOffset ToUtc(DateOnly date, TimeZoneInfo zone)
    {
        var midnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(midnight, zone.GetUtcOffset(midnight)).ToUniversalTime();
    }
}
