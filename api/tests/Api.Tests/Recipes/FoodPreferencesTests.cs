using Api.Entities;
using Api.Services.Recipes;

namespace Api.Tests.Recipes;

public class FoodPreferencesTests
{
    private static bool Finds(DislikedFood food, string text) => FoodPreferences.FindIn(text, [food], avoidSpicy: false) is not null;

    [Fact]
    public void EveryDislikedFood_HasALabelAndKeywords()
    {
        foreach (var food in Enum.GetValues<DislikedFood>())
        {
            var rule = FoodPreferences.Dislikes[food];
            Assert.NotEmpty(rule.Label);
            Assert.NotEmpty(rule.Keywords);
            // Chaque aliment se reconnaît au moins par son premier mot-clé.
            Assert.True(Finds(food, rule.Keywords[0]), $"{food} : « {rule.Keywords[0]} » non reconnu.");
        }
    }

    [Theory]
    // Mots entiers : « ail » ne trouve ni « caille » ni « travail ».
    [InlineData(DislikedFood.Garlic, "Gousse d'ail hachée", true)]
    [InlineData(DislikedFood.Garlic, "Cailles rôties", false)]
    [InlineData(DislikedFood.Garlic, "Un vrai travail de chef", false)]
    // Ingrédients composés.
    [InlineData(DislikedFood.Garlic, "Pâtes au pesto", true)]
    [InlineData(DislikedFood.Garlic, "Aïoli maison", true)]
    [InlineData(DislikedFood.Tomato, "Ketchup", true)]
    [InlineData(DislikedFood.Tomato, "Sauce tomate", true)]
    [InlineData(DislikedFood.Fish, "Nuoc-mâm", true)]
    [InlineData(DislikedFood.Fish, "nuoc mam", true)]
    [InlineData(DislikedFood.Fish, "Bâtonnets de surimi", true)]
    // Pluriels, accents, tirets.
    [InlineData(DislikedFood.Tomato, "Tomates cerises", true)]
    [InlineData(DislikedFood.Mushrooms, "Poêlée de CÈPES", true)]
    [InlineData(DislikedFood.Cauliflower, "Gratin de choux-fleurs", true)]
    [InlineData(DislikedFood.BrusselsSprouts, "Choux de Bruxelles rôtis", true)]
    [InlineData(DislikedFood.Leek, "Fondue de poireaux", true)]
    // Petits pois et pois chiches ne se confondent pas.
    [InlineData(DislikedFood.Peas, "Petits pois carottes", true)]
    [InlineData(DislikedFood.Peas, "Pois chiches rôtis", false)]
    [InlineData(DislikedFood.Legumes, "Pois chiches rôtis", true)]
    // Exceptions.
    [InlineData(DislikedFood.Olives, "Un filet d'huile d'olive", false)]
    [InlineData(DislikedFood.Olives, "Olives noires", true)]
    [InlineData(DislikedFood.Olives, "Huile d'olive et olives vertes", true)]
    [InlineData(DislikedFood.Coconut, "Haricots cocos", false)]
    [InlineData(DislikedFood.Coconut, "Cocos de Paimpol", false)]
    [InlineData(DislikedFood.Coconut, "Lait de coco", true)]
    [InlineData(DislikedFood.Coconut, "Poulet en cocotte", false)]
    // Mots courts ou ambigus volontairement absents.
    [InlineData(DislikedFood.Fish, "Un bar de ligne", false)]
    [InlineData(DislikedFood.BlueCheese, "Myrtilles bleues", false)]
    [InlineData(DislikedFood.BlueCheese, "Bleu d'Auvergne", true)]
    [InlineData(DislikedFood.Raisins, "Raisin blanc", false)]
    [InlineData(DislikedFood.Raisins, "Raisins secs", true)]
    [InlineData(DislikedFood.Lamb, "Gigot de sept heures", true)]
    // Mention négative : « sans », « ni », « pas de ».
    [InlineData(DislikedFood.Onion, "Servir sans oignon", false)]
    [InlineData(DislikedFood.Garlic, "Ni ail ni piment", false)]
    [InlineData(DislikedFood.Garlic, "Ne mets pas d'ail", false)]
    [InlineData(DislikedFood.Mushrooms, "Sans les champignons", false)]
    [InlineData(DislikedFood.Garlic, "Ajoute l'ail puis sers", true)]
    public void Keywords_AreMatchedAsWholeWords(DislikedFood food, string text, bool expected)
    {
        Assert.Equal(expected, Finds(food, text));
    }

    [Theory]
    [InlineData("Une cuillère de harissa", true)]
    [InlineData("Sauce piquante", true)]
    [InlineData("Piment d'Espelette", true)]
    [InlineData("Piment doux", false)]
    [InlineData("Sel et poivre", false)]   // le poivre reste un assaisonnement de base
    public void NotSpicy_RejectsHotSpices_ButNotPepper(string text, bool expected)
    {
        Assert.Equal(expected, FoodPreferences.FindIn(text, [], avoidSpicy: true) is not null);
    }

    [Fact]
    public void FindIn_ReturnsTheRuleAndItsKind_OrNullWithoutPreferences()
    {
        Assert.Equal(new PreferenceHit("champignons", PreferenceKind.Taste),
            FoodPreferences.FindIn("Champignons de Paris", [DislikedFood.Mushrooms], false));
        Assert.Equal(PreferenceKind.Constraint,
            FoodPreferences.FindIn("Pâtes fraîches", [], false, [Allergen.Gluten])?.Kind);
        Assert.Null(FoodPreferences.FindIn("Champignons de Paris", [], false));
        Assert.Null(FoodPreferences.FindIn("", [DislikedFood.Mushrooms], true));
    }

    // ---------- Contraintes : allergènes (profils et invités) et porc ----------

    private static bool FindsAllergen(Allergen allergen, string text) =>
        FoodPreferences.FindIn(text, [], false, [allergen]) is not null;

    [Theory]
    // Fruits à coque : « noix de » ne désigne pas toujours un fruit à coque.
    [InlineData(Allergen.TreeNuts, "Cerneaux de noix", true)]
    [InlineData(Allergen.TreeNuts, "Pâtes au pesto", true)]
    [InlineData(Allergen.TreeNuts, "Poudre d'amande", true)]
    [InlineData(Allergen.TreeNuts, "Lait de noix de coco", false)]
    [InlineData(Allergen.TreeNuts, "Une pincée de noix de muscade", false)]
    [InlineData(Allergen.TreeNuts, "Noix de Saint-Jacques poêlées", false)]
    [InlineData(Allergen.TreeNuts, "Noix de veau rôtie", false)]
    [InlineData(Allergen.TreeNuts, "Une noix de beurre", false)]
    [InlineData(Allergen.TreeNuts, "Pommes noisettes", false)]
    // Arachides.
    [InlineData(Allergen.Peanuts, "Beurre de cacahuète", true)]
    [InlineData(Allergen.Peanuts, "Huile d'arachide", true)]
    [InlineData(Allergen.Peanuts, "Sauce satay", true)]
    // Gluten : « pâte » au singulier, pâtes nommées, sauce soja ; « sans gluten » placé après.
    [InlineData(Allergen.Gluten, "Pâte brisée", true)]
    [InlineData(Allergen.Gluten, "Pâte à pizza", true)]
    [InlineData(Allergen.Gluten, "Spaghetti", true)]
    [InlineData(Allergen.Gluten, "Sauce soja", true)]
    [InlineData(Allergen.Gluten, "Chapelure", true)]
    [InlineData(Allergen.Gluten, "Pâtes sans gluten", false)]
    [InlineData(Allergen.Gluten, "Pain sans gluten", false)]
    [InlineData(Allergen.Gluten, "Pâte d'amande", false)]
    [InlineData(Allergen.Gluten, "Pâte de curry rouge", false)]
    [InlineData(Allergen.Gluten, "Farine de riz", false)]
    [InlineData(Allergen.Gluten, "Tamari", false)]
    [InlineData(Allergen.Gluten, "Polenta", false)]
    [InlineData(Allergen.Gluten, "Semoule de maïs", false)]
    [InlineData(Allergen.Gluten, "Nouilles de riz", false)]
    // Aucun produit laitier : « lait sans lactose » reste du lait.
    [InlineData(Allergen.Milk, "Crème fraîche", true)]
    [InlineData(Allergen.Milk, "Parmesan râpé", true)]
    [InlineData(Allergen.Milk, "Une noix de beurre", true)]
    [InlineData(Allergen.Milk, "Lait sans lactose", true)]
    [InlineData(Allergen.Milk, "Lait de coco", false)]
    [InlineData(Allergen.Milk, "Crème de coco", false)]
    [InlineData(Allergen.Milk, "Beurre de cacahuète", false)]
    [InlineData(Allergen.Milk, "Beurre d'amande", false)]
    [InlineData(Allergen.Milk, "Laitue", false)]
    public void Allergens_AreMatched_WithTheirExceptions(Allergen allergen, string text, bool expected)
    {
        Assert.Equal(expected, FindsAllergen(allergen, text));
    }

    [Theory]
    [InlineData("Lardons fumés", true)]
    [InlineData("Saindoux", true)]
    [InlineData("Tranches de pancetta", true)]
    [InlineData("Coppa", true)]
    [InlineData("Feuilles de gélatine", true)]
    [InlineData("Jambon de dinde", false)]
    [InlineData("Lardons de volaille", false)]
    [InlineData("Bacon de dinde", false)]
    [InlineData("Bacon de poulet", false)]
    [InlineData("Rillettes de thon", false)]
    [InlineData("Service en porcelaine", false)]
    public void NoPork_IsMatched_WithPoultryExceptions(string text, bool expected)
    {
        Assert.Equal(expected, FoodPreferences.FindIn(text, [], false, exclusions: [IngredientExclusion.Pork]) is not null);
    }

    [Fact]
    public void NotSpicyForGuestsOnly_IsAConstraint_NotATaste()
    {
        var profileSpicy = new Api.Services.Profiles.MealConstraints(
            Diet.Omnivore, [], [], CookingTime.NoLimit, MealBudget.NoLimit, NutritionGoal.Balanced, 1, []) { AvoidSpicy = true };
        var guestsSpicy = profileSpicy with { AvoidSpicyForThisMealOnly = true };

        Assert.Equal(PreferenceKind.Taste, FoodPreferences.FindIn("Harissa", profileSpicy)?.Kind);
        Assert.Equal(PreferenceKind.Constraint, FoodPreferences.FindIn("Harissa", guestsSpicy)?.Kind);
    }

    [Fact]
    public void AllergensWithoutKeywords_AreLeftToTheAi()
    {
        // Seuls 4 allergènes sont vérifiés par mots-clés ; les autres restent dans la consigne à l'IA.
        Assert.Null(FoodPreferences.FindIn("Graines de sésame", [], false, [Allergen.Sesame]));
    }

    [Fact]
    public void RejectionForAConstraint_NeverNamesIt()
    {
        var taste = new RecipeRejectedByPreferencesException(new PreferenceHit("ail", PreferenceKind.Taste));
        var constraint = new RecipeRejectedByPreferencesException(new PreferenceHit("arachides", PreferenceKind.Constraint));

        Assert.Equal("ail", taste.LoggableReason);
        Assert.Equal("contrainte du repas", constraint.LoggableReason);
        Assert.DoesNotContain("arachides", constraint.Message);
    }
}
