namespace Api.Services.Inventory;

/// <summary>
/// Estimation de la date de péremption à partir de la date d'achat et de la
/// durée de conservation de la catégorie. Fonction pure : aucune dépendance,
/// entièrement testable.
/// </summary>
public static class ExpiryEstimator
{
    public static DateOnly Estimate(DateOnly purchasedOn, int shelfLifeDays)
    {
        if (shelfLifeDays <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(shelfLifeDays), shelfLifeDays, "La durée de conservation doit être positive.");
        }

        // DateOnly : pas d'heure ni de fuseau horaire, donc aucun décalage possible
        // (changement d'heure, utilisateur à l'étranger…).
        return purchasedOn.AddDays(shelfLifeDays);
    }
}
