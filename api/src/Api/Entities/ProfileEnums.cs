namespace Api.Entities;

// Listes FERMÉES : ces valeurs finiront dans le prompt de l'IA (fonctionnalité 4).
// Aucun texte libre possible, donc aucune injection de prompt par le profil.
// Stockées en texte en base : lisibles, et robustes si on réordonne une énumération.

public enum CookingTime
{
    Under15Minutes,
    Under30Minutes,
    Under60Minutes,
    NoLimit,
}

public enum MealBudget
{
    Under2Euros,
    From2To4Euros,
    From4To7Euros,
    NoLimit,
}

// L'ordre de restriction (utile pour combiner les profils) est défini explicitement
// dans ProfileCombiner, pas déduit de l'ordre de déclaration.
public enum Diet
{
    Omnivore,
    Flexitarian,
    Pescatarian,
    Vegetarian,
    Vegan,
}

// Exclusions neutres : elles couvrent les pratiques religieuses sans les nommer
// (pas de « halal » ni « casher », qui révéleraient une conviction : donnée sensible).
public enum IngredientExclusion
{
    Pork,
    Beef,
    Offal,
    Seafood,
    Alcohol,
}

// Les 14 allergènes à déclaration obligatoire (règlement UE n° 1169/2011, annexe II).
public enum Allergen
{
    Gluten,
    Crustaceans,
    Eggs,
    Fish,
    Peanuts,
    Soybeans,
    Milk,
    TreeNuts,
    Celery,
    Mustard,
    Sesame,
    Sulphites,
    Lupin,
    Molluscs,
}

public enum NutritionGoal
{
    Balanced,
    MuscleGain,
    LightMeals,
    SimpleAntiWaste,
}

// Équipement de la cuisine du FOYER (partagée par ses membres).
public enum KitchenEquipment
{
    Hob,
    Oven,
    Microwave,
    AirFryer,
    Blender,
}
