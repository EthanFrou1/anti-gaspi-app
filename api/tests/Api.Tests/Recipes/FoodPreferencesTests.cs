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
    public void FindIn_ReturnsTheLabel_OrNullWithoutPreferences()
    {
        Assert.Equal("champignons", FoodPreferences.FindIn("Champignons de Paris", [DislikedFood.Mushrooms], false));
        Assert.Null(FoodPreferences.FindIn("Champignons de Paris", [], false));
        Assert.Null(FoodPreferences.FindIn("", [DislikedFood.Mushrooms], true));
    }
}
