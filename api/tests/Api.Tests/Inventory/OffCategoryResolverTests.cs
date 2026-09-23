using Api.Data.Seed;
using Api.Services.Inventory;

namespace Api.Tests.Inventory;

/// <summary>
/// Les exemples reprennent la forme réelle des « categories_tags » d'Open Food Facts
/// (du plus général au plus précis) et utilisent les vraies règles de l'app.
/// </summary>
public class OffCategoryResolverTests
{
    private static readonly IReadOnlyDictionary<string, OffTagRule> Rules =
        CategorySeed.OffMappings.ToDictionary(m => m.OffTag, m => new OffTagRule(m.CategoryId, m.Priority));

    private static int CategoryId(string code) => CategorySeed.Categories.Single(c => c.Code == code).Id;

    [Fact]
    public void Yogurt_IsMappedToYogurts()
    {
        string[] tags = ["en:dairies", "en:fermented-foods", "en:fermented-milk-products", "en:yogurts", "en:plain-yogurts"];

        Assert.Equal(CategoryId("yogurts"), OffCategoryResolver.Resolve(tags, Rules));
    }

    [Fact]
    public void MostSpecificKnownTagWins()
    {
        // « en:milks » (lait frais) est plus général que « en:uht-milks ».
        string[] tags = ["en:dairies", "en:milks", "en:uht-milks", "en:semi-skimmed-milks"];

        Assert.Equal(CategoryId("uht-milk"), OffCategoryResolver.Resolve(tags, Rules));
    }

    [Fact]
    public void CannedVegetables_AreStoredAsCanned()
    {
        // Le mode de conservation l'emporte sur la nature du produit, même placé avant.
        string[] tags = ["en:plant-based-foods", "en:canned-foods", "en:vegetables", "en:green-beans"];

        Assert.Equal(CategoryId("canned"), OffCategoryResolver.Resolve(tags, Rules));
    }

    [Fact]
    public void FrozenVegetables_AreStoredAsFrozen()
    {
        string[] tags = ["en:plant-based-foods", "en:vegetables", "en:frozen-foods", "en:frozen-vegetables", "en:peas"];

        Assert.Equal(CategoryId("frozen"), OffCategoryResolver.Resolve(tags, Rules));
    }

    [Fact]
    public void Butter_IsNotMistakenForCream()
    {
        string[] tags = ["en:dairies", "en:fats", "en:animal-fats", "en:milkfat", "en:butters", "en:salted-butters"];

        Assert.Equal(CategoryId("butter"), OffCategoryResolver.Resolve(tags, Rules));
    }

    [Fact]
    public void Camembert_IsASoftCheese()
    {
        string[] tags = ["en:dairies", "en:fermented-milk-products", "en:cheeses", "en:cow-cheeses", "en:french-cheeses",
            "en:soft-cheeses", "en:camemberts"];

        Assert.Equal(CategoryId("soft-cheese"), OffCategoryResolver.Resolve(tags, Rules));
    }

    [Fact]
    public void Comte_IsAHardCheese()
    {
        string[] tags = ["en:dairies", "en:fermented-milk-products", "en:cheeses", "en:hard-cheeses", "en:french-cheeses",
            "en:cow-cheeses", "en:comte"];

        Assert.Equal(CategoryId("hard-cheese"), OffCategoryResolver.Resolve(tags, Rules));
    }

    [Fact]
    public void UnspecifiedCheese_FallsBackToTheCautiousSoftCheese()
    {
        // « Produits laitiers fermentés » (yaourts) est plus général que « Fromages ».
        string[] tags = ["en:dairies", "en:fermented-milk-products", "en:cheeses"];

        Assert.Equal(CategoryId("soft-cheese"), OffCategoryResolver.Resolve(tags, Rules));
    }

    [Fact]
    public void TagsAreMatchedRegardlessOfCaseAndSpaces()
    {
        string[] tags = [" EN:Yogurts "];

        Assert.Equal(CategoryId("yogurts"), OffCategoryResolver.Resolve(tags, Rules));
    }

    [Fact]
    public void UnknownTags_ReturnNull()
    {
        string[] tags = ["en:unknown-category", "fr:produit-mystere"];

        Assert.Null(OffCategoryResolver.Resolve(tags, Rules));
    }

    [Fact]
    public void NoTags_ReturnNull()
    {
        Assert.Null(OffCategoryResolver.Resolve([], Rules));
    }
}
