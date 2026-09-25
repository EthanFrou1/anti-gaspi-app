using Api.Entities;
using Api.Services.Profiles;

namespace Api.Tests.Profiles;

/// <summary>
/// Règle « repas partagé » (CLAUDE.md) : si l'un est végétarien, la recette l'est ;
/// les allergies de tous sont exclues.
/// </summary>
public class ProfileCombinerTests
{
    private static readonly KitchenEquipment[] Kitchen = [KitchenEquipment.Hob, KitchenEquipment.Microwave];

    private static UserProfile Profile(
        Diet diet = Diet.Omnivore,
        Allergen[]? allergens = null,
        IngredientExclusion[]? exclusions = null,
        CookingTime time = CookingTime.NoLimit,
        MealBudget budget = MealBudget.NoLimit,
        NutritionGoal goal = NutritionGoal.Balanced,
        int servings = 1) => new()
        {
            Diet = diet,
            Allergens = [.. allergens ?? []],
            Exclusions = [.. exclusions ?? []],
            CookingTime = time,
            Budget = budget,
            Goal = goal,
            DefaultServings = servings,
        };

    [Fact]
    public void SinglePerson_KeepsTheirOwnConstraints()
    {
        var alice = Profile(Diet.Pescatarian, [Allergen.Peanuts], [IngredientExclusion.Alcohol],
            CookingTime.Under15Minutes, MealBudget.Under2Euros, NutritionGoal.MuscleGain, servings: 2);

        var constraints = ProfileCombiner.Combine([alice], Kitchen);

        Assert.Equal(Diet.Pescatarian, constraints.Diet);
        Assert.Equal([Allergen.Peanuts], constraints.Allergens);
        Assert.Equal([IngredientExclusion.Alcohol], constraints.Exclusions);
        Assert.Equal(CookingTime.Under15Minutes, constraints.CookingTime);
        Assert.Equal(MealBudget.Under2Euros, constraints.Budget);
        Assert.Equal(NutritionGoal.MuscleGain, constraints.Goal);
        // Seule, elle cuisine selon ses portions par défaut (ex. 2 pour avoir un reste).
        Assert.Equal(2, constraints.Servings);
    }

    [Fact]
    public void OneVegetarian_MakesTheMealVegetarian()
    {
        var constraints = ProfileCombiner.Combine([Profile(Diet.Omnivore), Profile(Diet.Vegetarian)], Kitchen);

        Assert.Equal(Diet.Vegetarian, constraints.Diet);
    }

    [Theory]
    [InlineData(Diet.Omnivore, Diet.Flexitarian, Diet.Flexitarian)]
    [InlineData(Diet.Pescatarian, Diet.Vegetarian, Diet.Vegetarian)] // le végétarien exclut aussi le poisson
    [InlineData(Diet.Vegetarian, Diet.Vegan, Diet.Vegan)]
    [InlineData(Diet.Vegan, Diet.Omnivore, Diet.Vegan)]              // l'ordre des convives ne compte pas
    [InlineData(Diet.Flexitarian, Diet.Pescatarian, Diet.Pescatarian)]
    public void MostRestrictiveDiet_Wins(Diet first, Diet second, Diet expected)
    {
        Assert.Equal(expected, ProfileCombiner.Combine([Profile(first), Profile(second)], Kitchen).Diet);
    }

    [Fact]
    public void AllergiesOfEveryone_AreExcluded()
    {
        var constraints = ProfileCombiner.Combine(
            [Profile(allergens: [Allergen.Peanuts, Allergen.Milk]), Profile(allergens: [Allergen.Milk, Allergen.Sesame]), Profile()],
            Kitchen);

        // Sans doublon, triés dans l'ordre de l'énumération (ordre stable pour le prompt de l'IA).
        Assert.Equal([Allergen.Peanuts, Allergen.Milk, Allergen.Sesame], constraints.Allergens);
    }

    [Fact]
    public void ExclusionsOfEveryone_AreCombined()
    {
        var constraints = ProfileCombiner.Combine(
            [Profile(exclusions: [IngredientExclusion.Pork]), Profile(exclusions: [IngredientExclusion.Alcohol, IngredientExclusion.Pork])],
            Kitchen);

        Assert.Equal([IngredientExclusion.Pork, IngredientExclusion.Alcohol], constraints.Exclusions);
    }

    [Fact]
    public void TastesOfEveryone_AreCombined_AndOneNotSpicyIsEnough()
    {
        var alice = new UserProfile { Dislikes = [DislikedFood.Olives, DislikedFood.Mushrooms], AvoidSpicy = true };
        var bob = new UserProfile { Dislikes = [DislikedFood.Mushrooms, DislikedFood.Fish] };

        var together = ProfileCombiner.Combine([alice, bob], Kitchen);
        var bobAlone = ProfileCombiner.Combine([bob], Kitchen);

        // Sans doublon, dans l'ordre de l'énumération.
        Assert.Equal([DislikedFood.Mushrooms, DislikedFood.Olives, DislikedFood.Fish], together.Dislikes);
        Assert.True(together.AvoidSpicy);
        Assert.False(bobAlone.AvoidSpicy);
    }

    [Fact]
    public void ShortestCookingTimeAndLowestBudget_Win()
    {
        var constraints = ProfileCombiner.Combine(
            [Profile(time: CookingTime.Under60Minutes, budget: MealBudget.Under2Euros),
             Profile(time: CookingTime.Under15Minutes, budget: MealBudget.NoLimit)],
            Kitchen);

        Assert.Equal(CookingTime.Under15Minutes, constraints.CookingTime);
        Assert.Equal(MealBudget.Under2Euros, constraints.Budget);
    }

    [Fact]
    public void SharedGoal_IsKept()
    {
        var constraints = ProfileCombiner.Combine(
            [Profile(goal: NutritionGoal.MuscleGain), Profile(goal: NutritionGoal.MuscleGain)], Kitchen);

        Assert.Equal(NutritionGoal.MuscleGain, constraints.Goal);
    }

    [Fact]
    public void DifferentGoals_FallBackToBalanced()
    {
        var constraints = ProfileCombiner.Combine(
            [Profile(goal: NutritionGoal.MuscleGain), Profile(goal: NutritionGoal.LightMeals)], Kitchen);

        Assert.Equal(NutritionGoal.Balanced, constraints.Goal);
    }

    [Fact]
    public void SharedMeal_HasOneServingPerDiner_UnlessSpecified()
    {
        var diners = new[] { Profile(servings: 2), Profile(servings: 4), Profile() };

        Assert.Equal(3, ProfileCombiner.Combine(diners, Kitchen).Servings);
        Assert.Equal(5, ProfileCombiner.Combine(diners, Kitchen, servings: 5).Servings);
    }

    [Fact]
    public void KitchenEquipment_IsTheHouseholdOne()
    {
        var constraints = ProfileCombiner.Combine([Profile()], [KitchenEquipment.Oven, KitchenEquipment.Hob, KitchenEquipment.Oven]);

        Assert.Equal([KitchenEquipment.Hob, KitchenEquipment.Oven], constraints.Equipment);
    }

    [Fact]
    public void NoDiner_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => ProfileCombiner.Combine([], Kitchen));
    }

    [Fact]
    public void EveryEnumValue_IsRanked()
    {
        // Garde-fou : ajouter un régime, un temps ou un budget sans le classer dans le combineur
        // ferait échouer ce test (plutôt que produire une combinaison fausse en production).
        foreach (var diet in Enum.GetValues<Diet>())
        {
            ProfileCombiner.Combine([Profile(diet)], Kitchen);
        }
        foreach (var time in Enum.GetValues<CookingTime>())
        {
            ProfileCombiner.Combine([Profile(time: time)], Kitchen);
        }
        foreach (var budget in Enum.GetValues<MealBudget>())
        {
            ProfileCombiner.Combine([Profile(budget: budget)], Kitchen);
        }
    }
}
