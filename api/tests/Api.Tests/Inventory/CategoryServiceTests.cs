using Api.Data.Seed;
using Api.Dtos.Inventory;
using Api.Services.Inventory;
using Api.Tests.Infrastructure;

namespace Api.Tests.Inventory;

/// <summary>
/// Vérifie que la migration a bien inséré les données de référence
/// et que le service les expose correctement.
/// </summary>
public class CategoryServiceTests(DatabaseFixture database) : DatabaseTestBase(database)
{
    [Fact]
    public async Task List_ReturnsAllSeededCategoriesInReferenceOrder()
    {
        var categories = await WithServiceAsync<ICategoryService, IReadOnlyList<CategoryDto>>(s =>
            s.ListAsync(CancellationToken.None));

        Assert.Equal(CategorySeed.Categories.Select(c => c.Id), categories.Select(c => c.Id));
        var butter = Assert.Single(categories, c => c.Code == "butter");
        Assert.Equal("Beurre", butter.Name);
        Assert.Equal(60, butter.DefaultShelfLifeDays);
    }

    [Fact]
    public async Task GetOffRules_ReturnsAllSeededMappings()
    {
        var rules = await WithServiceAsync<ICategoryService, IReadOnlyDictionary<string, OffTagRule>>(s =>
            s.GetOffRulesAsync(CancellationToken.None));

        Assert.Equal(CategorySeed.OffMappings.Length, rules.Count);
        Assert.Equal(CategorySeed.PriorityStorage, rules["en:canned-foods"].Priority);
    }
}
