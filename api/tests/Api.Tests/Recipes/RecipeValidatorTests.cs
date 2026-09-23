using Api.Entities;
using Api.Services.Recipes;
using static Api.Tests.Recipes.RecipePromptBuilderTests;

namespace Api.Tests.Recipes;

public class RecipeValidatorTests
{
    private static readonly RecipePrompt Prompt = RecipePromptBuilder.Build(
        Constraints(time: CookingTime.Under30Minutes, servings: 2),
        [Item("Courgette", "vegetables", 1), Item("Riz", "dry-goods", 200)],
        Today);

    private static RecipeDraft Draft(
        string? title = "Riz sauté à la courgette",
        int minutes = 20,
        int servings = 2,
        RecipeDraftIngredient[]? ingredients = null,
        string[]? steps = null) =>
        new(title, minutes, servings,
            ingredients ?? [new("Courgette", "1", "p1"), new("Riz", "150 g", "p2"), new("Huile", "1 c. à soupe", null)],
            steps ?? ["Cuire le riz.", "Faire sauter la courgette.", "Mélanger."]);

    [Fact]
    public void ValidDraft_MapsReferencesToRealProductIds()
    {
        var recipe = RecipeValidator.Validate(Draft(), Prompt);

        Assert.Equal(Prompt.Items[0].Item.Id, recipe.Ingredients[0].InventoryItemId);
        Assert.Equal(Prompt.Items[1].Item.Id, recipe.Ingredients[1].InventoryItemId);
        Assert.Null(recipe.Ingredients[2].InventoryItemId);
    }

    [Fact]
    public void InventedReference_IsRejected()
    {
        var draft = Draft(ingredients: [new("Poulet", "200 g", "p99")]);

        Assert.Throws<RecipeGenerationUnavailableException>(() => RecipeValidator.Validate(draft, Prompt));
    }

    [Fact]
    public void TooLongPreparation_ForTheConstraint_IsRejected()
    {
        Assert.Throws<RecipeGenerationUnavailableException>(() => RecipeValidator.Validate(Draft(minutes: 45), Prompt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MissingTitle_IsRejected(string? title)
    {
        Assert.Throws<RecipeGenerationUnavailableException>(() => RecipeValidator.Validate(Draft(title: title), Prompt));
    }

    [Fact]
    public void NoStepsOrTooManySteps_AreRejected()
    {
        Assert.Throws<RecipeGenerationUnavailableException>(() => RecipeValidator.Validate(Draft(steps: []), Prompt));
        var tooMany = Enumerable.Repeat("Étape.", RecipeValidator.MaxSteps + 1).ToArray();
        Assert.Throws<RecipeGenerationUnavailableException>(() => RecipeValidator.Validate(Draft(steps: tooMany), Prompt));
    }

    [Fact]
    public void Servings_AlwaysComeFromTheRequest_NotFromTheModel()
    {
        Assert.Equal(2, RecipeValidator.Validate(Draft(servings: 6), Prompt).Servings);
    }

    [Fact]
    public void FakeGenerator_ProducesAValidRecipe_WithTheMostUrgentProducts()
    {
        var draft = new FakeRecipeGenerator().GenerateAsync(Prompt, CancellationToken.None).Result;

        var recipe = RecipeValidator.Validate(draft, Prompt);

        Assert.Equal(Prompt.Items[0].Item.Id, recipe.Ingredients[0].InventoryItemId);
        Assert.Contains("courgette", recipe.Title);
    }

    [Fact]
    public void FakeGenerator_WithAnEmptyFridge_StillProducesAValidRecipe()
    {
        var emptyPrompt = RecipePromptBuilder.Build(Constraints(), [], Today);

        var draft = new FakeRecipeGenerator().GenerateAsync(emptyPrompt, CancellationToken.None).Result;

        Assert.NotNull(RecipeValidator.Validate(draft, emptyPrompt));
    }
}
