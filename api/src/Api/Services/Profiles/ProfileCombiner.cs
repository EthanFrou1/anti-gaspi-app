using Api.Entities;

namespace Api.Services.Profiles;

/// <summary>
/// Contraintes d'un repas : ce que la recette générée (fonctionnalité 4) devra respecter.
/// Ne contient aucune identité : c'est ce qui sera transmis à l'IA.
/// </summary>
public sealed record MealConstraints(
    Diet Diet,
    IReadOnlyList<IngredientExclusion> Exclusions,
    IReadOnlyList<Allergen> Allergens,
    CookingTime CookingTime,
    MealBudget Budget,
    NutritionGoal Goal,
    int Servings,
    IReadOnlyList<KitchenEquipment> Equipment)
{
    // Goûts des convives (aucun nom : on ne sait pas qui n'aime quoi).
    public IReadOnlyList<DislikedFood> Dislikes { get; init; } = [];

    public bool AvoidSpicy { get; init; }
}

/// <summary>
/// Combine les profils des personnes qui mangent ensemble (règle « repas partagé » de CLAUDE.md).
/// Principe : la recette doit convenir à TOUT le monde, donc chaque contrainte prend
/// la valeur la plus stricte. Fonction pure, entièrement testable.
/// </summary>
public static class ProfileCombiner
{
    // Du moins au plus restrictif. Chaque régime autorise un sous-ensemble du précédent
    // (le végétarien exclut aussi le poisson que le pescétarien accepte), donc « le plus
    // restrictif » convient à tous. Ordre explicite : il ne dépend pas de l'ordre de l'enum.
    private static readonly Diet[] DietStrictness =
        [Diet.Omnivore, Diet.Flexitarian, Diet.Pescatarian, Diet.Vegetarian, Diet.Vegan];

    // Du plus contraignant au plus souple.
    private static readonly CookingTime[] CookingTimeStrictness =
        [CookingTime.Under15Minutes, CookingTime.Under30Minutes, CookingTime.Under60Minutes, CookingTime.NoLimit];

    private static readonly MealBudget[] BudgetStrictness =
        [MealBudget.Under2Euros, MealBudget.From2To4Euros, MealBudget.From4To7Euros, MealBudget.NoLimit];

    /// <param name="diners">Profils des personnes qui mangent (au moins une).</param>
    /// <param name="kitchenEquipment">Équipement du foyer (cuisine partagée, rien à combiner).</param>
    /// <param name="servings">
    /// Nombre de portions demandé. Par défaut : pour une personne seule, ses portions par défaut
    /// (elle peut cuisiner pour deux repas) ; pour un repas partagé, une portion par convive.
    /// </param>
    public static MealConstraints Combine(
        IReadOnlyCollection<UserProfile> diners,
        IReadOnlyList<KitchenEquipment> kitchenEquipment,
        int? servings = null)
    {
        if (diners.Count == 0)
        {
            throw new ArgumentException("Il faut au moins une personne à table.", nameof(diners));
        }

        return new MealConstraints(
            Diet: MostRestrictive(diners.Select(d => d.Diet), DietStrictness, strictestIsLast: true),
            // Union : ce que l'un exclut, personne n'en mange.
            Exclusions: diners.SelectMany(d => d.Exclusions).Distinct().Order().ToList(),
            // Union : les allergies de tous sont exclues (sécurité avant tout).
            Allergens: diners.SelectMany(d => d.Allergens).Distinct().Order().ToList(),
            CookingTime: MostRestrictive(diners.Select(d => d.CookingTime), CookingTimeStrictness, strictestIsLast: false),
            Budget: MostRestrictive(diners.Select(d => d.Budget), BudgetStrictness, strictestIsLast: false),
            Goal: CombineGoals(diners.Select(d => d.Goal).ToList()),
            Servings: servings ?? (diners.Count == 1 ? diners.First().DefaultServings : diners.Count),
            Equipment: kitchenEquipment.Distinct().Order().ToList())
        {
            // Union : ce qu'un convive n'aime pas n'est pas cuisiné pour la tablée.
            Dislikes = diners.SelectMany(d => d.Dislikes).Distinct().Order().ToList(),
            // Il suffit d'un convive qui ne supporte pas l'épicé.
            AvoidSpicy = diners.Any(d => d.AvoidSpicy),
        };
    }

    /// <summary>
    /// Ajoute les contraintes d'un repas (invités) à celles des convives : elles ne peuvent que
    /// resserrer (« Végétarien » laisse végan un repas déjà végan). Rien n'est conservé.
    /// </summary>
    public static MealConstraints WithMealRestrictions(MealConstraints constraints, IReadOnlyCollection<MealRestriction> restrictions)
    {
        if (restrictions.Count == 0)
        {
            return constraints;
        }

        var extraAllergens = restrictions.Select(r => r switch
        {
            MealRestriction.NoTreeNuts => Allergen.TreeNuts,
            MealRestriction.NoPeanuts => Allergen.Peanuts,
            MealRestriction.NoGluten => Allergen.Gluten,
            MealRestriction.NoDairy => Allergen.Milk,
            _ => (Allergen?)null,
        }).OfType<Allergen>();

        return constraints with
        {
            Diet = restrictions.Contains(MealRestriction.Vegetarian)
                ? MostRestrictive([constraints.Diet, Diet.Vegetarian], DietStrictness, strictestIsLast: true)
                : constraints.Diet,
            Exclusions = restrictions.Contains(MealRestriction.NoPork)
                ? constraints.Exclusions.Append(IngredientExclusion.Pork).Distinct().Order().ToList()
                : constraints.Exclusions,
            Allergens = constraints.Allergens.Concat(extraAllergens).Distinct().Order().ToList(),
            AvoidSpicy = constraints.AvoidSpicy || restrictions.Contains(MealRestriction.NotSpicy),
        };
    }

    // Objectifs incompatibles entre eux (prise de muscle vs repas légers) : si tout le monde
    // n'a pas le même, on retient « équilibré », qui convient à chacun.
    private static NutritionGoal CombineGoals(IReadOnlyList<NutritionGoal> goals) =>
        goals.Distinct().Count() == 1 ? goals[0] : NutritionGoal.Balanced;

    private static T MostRestrictive<T>(IEnumerable<T> values, T[] strictness, bool strictestIsLast)
        where T : struct, Enum
    {
        var ranks = values.Select(v =>
        {
            var rank = Array.IndexOf(strictness, v);
            // Nouvelle valeur d'enum oubliée ici : on préfère une erreur franche à un résultat faux.
            return rank >= 0 ? rank : throw new InvalidOperationException($"Valeur non classée : {v}.");
        });
        var rank = strictestIsLast ? ranks.Max() : ranks.Min();
        return strictness[rank];
    }
}
