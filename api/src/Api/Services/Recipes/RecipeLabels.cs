using Api.Entities;

namespace Api.Services.Recipes;

/// <summary>
/// Libellés français des contraintes, tels que lus par l'IA. Une valeur d'énumération
/// oubliée lève une exception ; un test parcourt toutes les valeurs pour le détecter
/// avant la mise en production.
/// </summary>
public static class RecipeLabels
{
    public static string Diet(Entities.Diet diet) => diet switch
    {
        Entities.Diet.Omnivore => "omnivore",
        Entities.Diet.Flexitarian => "flexitarien (peu de viande)",
        Entities.Diet.Pescatarian => "pescétarien (poisson autorisé, pas de viande)",
        Entities.Diet.Vegetarian => "végétarien (ni viande ni poisson)",
        Entities.Diet.Vegan => "végan (aucun produit d'origine animale)",
        _ => throw new ArgumentOutOfRangeException(nameof(diet)),
    };

    public static string Exclusion(IngredientExclusion exclusion) => exclusion switch
    {
        IngredientExclusion.Pork => "porc",
        IngredientExclusion.Beef => "bœuf",
        IngredientExclusion.Offal => "abats",
        IngredientExclusion.Seafood => "fruits de mer",
        IngredientExclusion.Alcohol => "alcool",
        _ => throw new ArgumentOutOfRangeException(nameof(exclusion)),
    };

    public static string Allergen(Entities.Allergen allergen) => allergen switch
    {
        Entities.Allergen.Gluten => "gluten",
        Entities.Allergen.Crustaceans => "crustacés",
        Entities.Allergen.Eggs => "œufs",
        Entities.Allergen.Fish => "poisson",
        Entities.Allergen.Peanuts => "arachides",
        Entities.Allergen.Soybeans => "soja",
        Entities.Allergen.Milk => "lait",
        Entities.Allergen.TreeNuts => "fruits à coque",
        Entities.Allergen.Celery => "céleri",
        Entities.Allergen.Mustard => "moutarde",
        Entities.Allergen.Sesame => "sésame",
        Entities.Allergen.Sulphites => "sulfites",
        Entities.Allergen.Lupin => "lupin",
        Entities.Allergen.Molluscs => "mollusques",
        _ => throw new ArgumentOutOfRangeException(nameof(allergen)),
    };

    public static string Dislike(DislikedFood food) =>
        FoodPreferences.Dislikes.TryGetValue(food, out var rule)
            ? rule.Label
            : throw new ArgumentOutOfRangeException(nameof(food));

    /// <summary>Temps maximal en minutes ; 240 pour « pas de limite » (borne de validation).</summary>
    public static int MaxMinutes(CookingTime time) => time switch
    {
        CookingTime.Under15Minutes => 15,
        CookingTime.Under30Minutes => 30,
        CookingTime.Under60Minutes => 60,
        CookingTime.NoLimit => 240,
        _ => throw new ArgumentOutOfRangeException(nameof(time)),
    };

    public static string Budget(MealBudget budget) => budget switch
    {
        MealBudget.Under2Euros => "moins de 2 € par portion",
        MealBudget.From2To4Euros => "2 à 4 € par portion",
        MealBudget.From4To7Euros => "4 à 7 € par portion",
        MealBudget.NoLimit => "pas de limite",
        _ => throw new ArgumentOutOfRangeException(nameof(budget)),
    };

    public static string Goal(NutritionGoal goal) => goal switch
    {
        NutritionGoal.Balanced => "repas équilibré",
        NutritionGoal.MuscleGain => "riche en protéines (prise de muscle)",
        NutritionGoal.LightMeals => "repas léger",
        NutritionGoal.SimpleAntiWaste => "simple, l'essentiel est de ne rien gaspiller",
        _ => throw new ArgumentOutOfRangeException(nameof(goal)),
    };

    public static string Equipment(KitchenEquipment equipment) => equipment switch
    {
        KitchenEquipment.Hob => "plaques de cuisson",
        KitchenEquipment.Oven => "four",
        KitchenEquipment.Microwave => "micro-ondes",
        KitchenEquipment.AirFryer => "air fryer",
        KitchenEquipment.Blender => "blender / mixeur",
        _ => throw new ArgumentOutOfRangeException(nameof(equipment)),
    };

    public static string Unit(QuantityUnit unit) => unit switch
    {
        QuantityUnit.Piece => "pièce(s)",
        QuantityUnit.Gram => "g",
        QuantityUnit.Kilogram => "kg",
        QuantityUnit.Milliliter => "ml",
        QuantityUnit.Liter => "l",
        _ => throw new ArgumentOutOfRangeException(nameof(unit)),
    };
}
