using Api.Data.Seed;
using Api.Entities;

namespace Api.Tests.Inventory;

/// <summary>
/// Garde-fous sur les données de référence : une erreur ici fausserait
/// toutes les dates estimées.
/// </summary>
public class CategorySeedTests
{
    [Fact]
    public void Categories_HaveUniqueIdsAndCodes()
    {
        Assert.Equal(CategorySeed.Categories.Length, CategorySeed.Categories.Select(c => c.Id).Distinct().Count());
        Assert.Equal(CategorySeed.Categories.Length, CategorySeed.Categories.Select(c => c.Code).Distinct().Count());
    }

    [Fact]
    public void Categories_HavePositiveShelfLife()
    {
        Assert.All(CategorySeed.Categories, c => Assert.True(c.DefaultShelfLifeDays > 0, c.Code));
    }

    [Fact]
    public void Mappings_ReferenceExistingCategories()
    {
        var ids = CategorySeed.Categories.Select(c => c.Id).ToHashSet();

        Assert.All(CategorySeed.OffMappings, m => Assert.Contains(m.CategoryId, ids));
    }

    [Fact]
    public void Mappings_UseNormalizedOffTags()
    {
        Assert.All(CategorySeed.OffMappings, m =>
        {
            Assert.StartsWith("en:", m.OffTag);
            Assert.Equal(m.OffTag.ToLowerInvariant(), m.OffTag);
        });
        Assert.Equal(CategorySeed.OffMappings.Length, CategorySeed.OffMappings.Select(m => m.OffTag).Distinct().Count());
    }

    [Theory]
    [InlineData("butter", ExpiryKind.BestBefore)]
    [InlineData("cream", ExpiryKind.UseBy)]
    [InlineData("ground-meat", ExpiryKind.UseBy)]
    [InlineData("dry-goods", ExpiryKind.BestBefore)]
    [InlineData("soft-cheese", ExpiryKind.UseBy)]
    [InlineData("hard-cheese", ExpiryKind.BestBefore)]
    public void Categories_HaveTheExpectedExpiryKind(string code, ExpiryKind expected)
    {
        Assert.Equal(expected, CategorySeed.Categories.Single(c => c.Code == code).ExpiryKind);
    }

    [Theory]
    [InlineData("butter", "cream")]
    [InlineData("hard-cheese", "soft-cheese")]
    public void LongerKeepingCategory_HasLongerShelfLife(string longer, string shorter)
    {
        var longerCategory = CategorySeed.Categories.Single(c => c.Code == longer);
        var shorterCategory = CategorySeed.Categories.Single(c => c.Code == shorter);

        Assert.True(longerCategory.DefaultShelfLifeDays > shorterCategory.DefaultShelfLifeDays);
    }
}
