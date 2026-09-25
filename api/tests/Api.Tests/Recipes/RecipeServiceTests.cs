using Api.Common;
using Api.Data;
using Api.Data.Seed;
using Api.Dtos.Inventory;
using Api.Dtos.Profiles;
using Api.Dtos.Recipes;
using Api.Entities;
using Api.Options;
using Api.Services.Ai;
using Api.Services.Inventory;
using Api.Services.Profiles;
using Api.Services.Recipes;
using Api.Tests.Households;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests.Recipes;

public class RecipeServiceTests(DatabaseFixture database) : HouseholdTestBase(database)
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    private DateOnly Today => QuotaDay.Window(Clock.Now, Paris).LocalDate;

    // ---------- Génération ----------

    [Fact]
    public async Task Generate_ReturnsARecipe_UsingTheMostUrgentProducts()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();

        var result = await GenerateAsync(alice, householdId);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var urgent = result.Value.Ingredients.First(i => i.InventoryItemId is not null);
        Assert.Equal("Steak haché", urgent.Name);
    }

    [Fact]
    public async Task Generate_StoresOnlyTheRecipe_InTheHistory()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        await SaveProfileAsync(alice, Diet.Omnivore, [Allergen.Peanuts]);

        await GenerateAsync(alice, householdId);

        await using var db = CreateDbContext();
        var stored = await db.RecipeGenerations.SingleAsync();
        Assert.Contains("\"title\"", stored.RecipeJson);
        // Ni contraintes (allergies…), ni prompt, ni inventaire complet.
        Assert.DoesNotContain("arachides", stored.RecipeJson);
        Assert.DoesNotContain("allergensToAvoid", stored.RecipeJson);
    }

    [Fact]
    public async Task SharedMeal_CombinesTheDinersProfiles_BeforeCallingTheAi()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        var bob = await CreateUserAsync("Bob");
        await AddMemberAsync(alice, householdId, bob);
        await SaveProfileAsync(bob, Diet.Vegetarian, [Allergen.Milk]);

        await GenerateAsync(alice, householdId, new GenerateRecipeRequest([alice, bob], null));

        var prompt = Generator.LastPrompt!;
        Assert.Equal(Diet.Vegetarian, prompt.Constraints.Diet);
        Assert.Contains(Allergen.Milk, prompt.Constraints.Allergens);
        Assert.Equal(2, prompt.Constraints.Servings);
        // Le steak haché (viande) et le yaourt (lait) ne sont même pas proposés à l'IA.
        Assert.DoesNotContain(prompt.Items, i => i.Item.Name is "Steak haché" or "Yaourt");
    }

    [Fact]
    public async Task DinerOutsideTheHousehold_IsRejected()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        var mallory = await CreateUserAsync("Mallory");

        var result = await GenerateAsync(alice, householdId, new GenerateRecipeRequest([alice, mallory], null));

        Assert.Equal(RecipeErrors.InvalidDiners, result.Error);
        Assert.Equal(0, Generator.Calls);
    }

    // ---------- Quotas ----------

    [Fact]
    public async Task FourthGenerationOfTheDay_IsRefused()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        for (var i = 0; i < 3; i++)
        {
            Assert.True((await GenerateAsync(alice, householdId)).IsSuccess);
        }

        var fourth = await GenerateAsync(alice, householdId);

        Assert.Equal(RecipeErrors.DailyQuotaReached, fourth.Error);
        Assert.Equal(3, Generator.Calls); // l'IA n'est pas appelée une 4e fois
        var quota = await QuotaAsync(alice);
        Assert.Equal((3, 3, 0), (quota.Used, quota.Limit, quota.Remaining));
    }

    [Fact]
    public async Task Quota_ResetsAtMidnightParisTime()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        for (var i = 0; i < 3; i++)
        {
            await GenerateAsync(alice, householdId);
        }

        var nextMidnight = QuotaDay.Window(Clock.Now, Paris).EndUtc;
        Clock.Advance(nextMidnight - Clock.Now + TimeSpan.FromMinutes(1));

        Assert.True((await GenerateAsync(alice, householdId)).IsSuccess);
    }

    [Fact]
    public async Task GlobalDailyLimit_AppliesAcrossAllUsers()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        var bob = await CreateUserAsync("Bob");
        await AddMemberAsync(alice, householdId, bob);
        var limited = new AiOptions { GlobalDailyLimit = 2 };

        Assert.True((await GenerateAsync(alice, householdId, options: limited)).IsSuccess);
        Assert.True((await GenerateAsync(bob, householdId, options: limited)).IsSuccess);
        var third = await GenerateAsync(alice, householdId, options: limited);

        Assert.Equal(RecipeErrors.GlobalQuotaReached, third.Error);
    }

    [Fact]
    public async Task FailedGeneration_IsNotCounted()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        Generator.FailWithUnavailable = true;

        var result = await GenerateAsync(alice, householdId);

        Assert.Equal(RecipeErrors.Unavailable, result.Error);
        Assert.Equal(0, (await QuotaAsync(alice)).Used);
        await using var db = CreateDbContext();
        Assert.False(await db.RecipeGenerations.AnyAsync());
    }

    [Fact]
    public async Task InvalidAiResponse_IsRejected_AndNotCounted()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        // L'IA invente une référence de produit qui n'existe pas.
        Generator.Override = _ => new RecipeDraft("Recette", 10, 1, [new("Caviar", "1 boîte", "p999")], ["Servir."]);

        var result = await GenerateAsync(alice, householdId);

        Assert.Equal(RecipeErrors.Unavailable, result.Error);
        Assert.Equal(0, (await QuotaAsync(alice)).Used);
    }

    // ---------- Goûts des convives ----------

    [Fact]
    public async Task ADinersTastes_KeepProductsAwayFromTheAi_AndAreReportedWithoutSayingWhose()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        var bob = await CreateUserAsync("Bob");
        await AddMemberAsync(alice, householdId, bob);
        await SaveTastesAsync(bob, [DislikedFood.Zucchini]);

        var result = await GenerateAsync(alice, householdId, new GenerateRecipeRequest([alice, bob], null));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.DoesNotContain(Generator.LastPrompt!.Items, i => i.Item.Name == "Courgette");
        Assert.Equal(["Courgette"], result.Value.ExcludedByPreferences);
        // Conservé avec la recette (historique, favoris), toujours sans nom de convive.
        var stored = (await WithServiceAsync<IRecipeService, IReadOnlyList<RecipeDto>>(s =>
            s.ListAsync(alice, householdId, CancellationToken.None))).Single();
        Assert.Equal(["Courgette"], stored.ExcludedByPreferences);
    }

    [Fact]
    public async Task RecipeWithADislikedFood_IsRejected_AndNotCounted()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        await SaveTastesAsync(alice, [DislikedFood.Garlic]);
        // L'IA ignore la consigne « ne jamais utiliser » : le validateur rattrape.
        Generator.Override = _ => new RecipeDraft("Pâtes à l'ail", 10, 1, [new("Pâtes", "200 g", "p2"), new("Ail", "2 gousses", null)], ["Cuire."]);

        var result = await GenerateAsync(alice, householdId);

        Assert.Equal(RecipeErrors.Unavailable, result.Error);
        Assert.Equal(0, (await QuotaAsync(alice)).Used);
    }

    // ---------- Invités et contraintes du repas ----------

    [Fact]
    public async Task Guests_AddServings_AndTheirRestrictionsApply_WithoutBeingStored()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();

        var result = await GenerateAsync(alice, householdId, new GenerateRecipeRequest(null, null, Guests: 2,
            MealRestrictions: [MealRestriction.Vegetarian, MealRestriction.NoGluten, MealRestriction.NoPeanuts]));

        Assert.True(result.IsSuccess, result.Error?.Message);
        var prompt = Generator.LastPrompt!;
        Assert.Equal(3, prompt.Constraints.Servings);
        Assert.Equal(Diet.Vegetarian, prompt.Constraints.Diet);
        Assert.Contains(Allergen.Gluten, prompt.Constraints.Allergens);
        // Le steak (viande) et les pâtes (gluten) ne partent pas vers l'IA…
        Assert.DoesNotContain(prompt.Items, i => i.Item.Name is "Steak haché" or "Pâtes");
        // …et, écartés pour une contrainte, ils ne sont pas cités avec la recette.
        Assert.Empty(result.Value.ExcludedByPreferences);
        await using var db = CreateDbContext();
        var stored = (await db.RecipeGenerations.SingleAsync()).RecipeJson!;
        Assert.DoesNotContain("gluten", stored, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("arachide", stored, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecipeBreakingAMealRestriction_IsRejected_AndTheLogsNeverNameTheRestriction()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        var logs = new CapturingLoggerProvider();
        using var factory = new Microsoft.Extensions.Logging.LoggerFactory([logs]);
        Generator.Override = _ => new RecipeDraft("Tartines", 10, 2, [new("Pain de campagne", "4 tranches", null)], ["Griller le pain."]);

        await using var scope = CreateScope();
        var service = new RecipeService(scope.ServiceProvider.GetRequiredService<AppDbContext>(), Generator,
            Microsoft.Extensions.Options.Options.Create(new AiOptions()), Clock,
            new Microsoft.Extensions.Logging.Logger<RecipeService>(factory));
        var result = await service.GenerateAsync(alice, householdId,
            new GenerateRecipeRequest(null, null, Guests: 1, MealRestrictions: [MealRestriction.NoGluten]), CancellationToken.None);

        Assert.Equal(RecipeErrors.Unavailable, result.Error);
        Assert.Equal(0, (await QuotaAsync(alice)).Used);
        Assert.Contains("contrainte du repas", logs.AllText);
        Assert.DoesNotContain("gluten", logs.AllText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MoreThanTwelvePeople_OrAnUnknownRestriction_IsRejected_WithoutCallingTheAi()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();

        var crowd = await GenerateAsync(alice, householdId, new GenerateRecipeRequest(null, null, Guests: 12));
        var unknown = await GenerateAsync(alice, householdId,
            new GenerateRecipeRequest(null, null, Guests: 1, MealRestrictions: [(MealRestriction)99]));

        Assert.Equal(RecipeErrors.TooManyServings, crowd.Error);
        Assert.Equal(RecipeErrors.UnknownMealRestriction, unknown.Error);
        Assert.Equal(0, Generator.Calls);
    }

    [Fact]
    public async Task SimultaneousRequests_NeverExceedTheDailyQuota()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        // L'IA « met du temps » : les 6 demandes se chevauchent vraiment.
        Generator.Delay = TimeSpan.FromMilliseconds(300);

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => GenerateAsync(alice, householdId)));

        Assert.Equal(3, results.Count(r => r.IsSuccess));
        Assert.Equal(3, results.Count(r => r.Error == RecipeErrors.DailyQuotaReached));
    }

    [Fact]
    public async Task PromptPreview_DoesNotCallTheAi_NorCountInTheQuota()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();

        var preview = await WithServiceAsync<IRecipeService, Result<RecipePromptPreviewDto>>(s =>
            s.PreviewPromptAsync(alice, householdId, new GenerateRecipeRequest(null, null), CancellationToken.None));

        Assert.True(preview.IsSuccess);
        Assert.Contains("Steak haché", preview.Value.UserContent);
        Assert.Contains("PROMPT SYSTÈME", preview.Value.ClipboardText);
        Assert.Equal(0, Generator.Calls);
        Assert.Equal(0, (await QuotaAsync(alice)).Used);
    }

    // ---------- Historique ----------

    [Fact]
    public async Task RecipeOfAnotherHousehold_IsNotFound()
    {
        var (alice, aliceHousehold) = await HouseholdWithFridgeAsync();
        var mallory = await CreateUserAsync("Mallory");
        var malloryHousehold = await CreateHouseholdAsync(mallory, "Chez Mallory");
        var recipe = (await GenerateAsync(alice, aliceHousehold)).Value!;

        var result = await WithServiceAsync<IRecipeService, Result<RecipeDto>>(s =>
            s.GetAsync(mallory, malloryHousehold, recipe.Id, CancellationToken.None));

        Assert.Equal(RecipeErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Purge_RemovesOldHistory_AndAbandonedReservations()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        await GenerateAsync(alice, householdId);
        await using (var db = CreateDbContext())
        {
            // Réservation restée vide (crash pendant un appel à l'IA).
            db.RecipeGenerations.Add(new RecipeGeneration { HouseholdId = householdId, RequestedByUserId = alice, CreatedAt = Clock.Now });
            await db.SaveChangesAsync();
        }

        Clock.Advance(TimeSpan.FromMinutes(15));
        var afterFifteenMinutes = await PurgeAsync();
        Clock.Advance(TimeSpan.FromDays(31));
        var afterAMonth = await PurgeAsync();

        Assert.Equal(1, afterFifteenMinutes); // seule la réservation abandonnée
        Assert.Equal(1, afterAMonth);         // puis la recette, au-delà de 30 jours
    }

    // ---------- « J'ai cuisiné cette recette » ----------

    [Fact]
    public async Task MarkCooked_ConsumesOnlyTheCheckedProducts()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        var recipe = (await GenerateAsync(alice, householdId)).Value!;
        var used = recipe.Ingredients.Select(i => i.InventoryItemId).OfType<Guid>().ToList();

        var result = await MarkCookedAsync(alice, householdId, recipe.Id, [used[0]]);

        Assert.Equal(1, result.Value);
        await using var db = CreateDbContext();
        Assert.Equal(InventoryItemStatus.Consumed, (await db.InventoryItems.SingleAsync(i => i.Id == used[0])).Status);
        Assert.Equal(InventoryItemStatus.Active, (await db.InventoryItems.SingleAsync(i => i.Id == used[1])).Status);
    }

    [Fact]
    public async Task MarkCooked_RejectsProductsThatAreNotInTheRecipe()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        var recipe = (await GenerateAsync(alice, householdId)).Value!;
        var other = await AddItemAsync(alice, householdId, "Confiture", "condiments", daysLeft: 300);

        var result = await MarkCookedAsync(alice, householdId, recipe.Id, [other]);

        Assert.Equal(RecipeErrors.InvalidCookedItems, result.Error);
    }

    [Fact]
    public async Task MarkCooked_CannotConsumeAnotherMembersPersonalProduct()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var householdId = await CreateHouseholdAsync(alice);
        await AddMemberAsync(alice, householdId, bob);
        var bobsSteak = await AddItemAsync(bob, householdId, "Steak de Bob", "fresh-meat", daysLeft: 1, personal: true);
        var recipe = (await GenerateAsync(alice, householdId)).Value!;

        var result = await MarkCookedAsync(alice, householdId, recipe.Id, [bobsSteak]);

        Assert.Equal(InventoryErrors.NotOwner, result.Error);
    }

    // ---------- Favoris ----------

    [Fact]
    public async Task Favorite_IsVisibleToTheWholeHousehold_AsASharedCookbook()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        var bob = await CreateUserAsync("Bob");
        await AddMemberAsync(alice, householdId, bob);
        var recipe = (await GenerateAsync(alice, householdId)).Value!;

        var starred = await AddFavoriteAsync(alice, householdId, recipe.Id);
        var seenByBob = Assert.Single(await FavoritesAsync(bob, householdId));

        Assert.True(starred.Value!.IsFavorite);
        Assert.Equal(recipe.Id, seenByBob.Id);
        Assert.False(seenByBob.IsFavorite); // pas SON étoile
        Assert.Equal(1, seenByBob.FavoriteCount);
    }

    [Fact]
    public async Task FavoriteRecipe_IsKeptBeyondThirtyDays_UntilItsLastStarIsRemoved()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        var recipe = (await GenerateAsync(alice, householdId)).Value!;
        await AddFavoriteAsync(alice, householdId, recipe.Id);

        Clock.Advance(TimeSpan.FromDays(45));
        await PurgeAsync();
        Assert.Single(await FavoritesAsync(alice, householdId));
        Assert.Empty(await WithServiceAsync<IRecipeService, IReadOnlyList<RecipeDto>>(s =>
            s.ListAsync(alice, householdId, CancellationToken.None))); // plus dans « récentes »

        await WithServiceAsync<IRecipeService, Result<RecipeDto>>(s =>
            s.RemoveFavoriteAsync(alice, householdId, recipe.Id, CancellationToken.None));
        Assert.Equal(1, await PurgeAsync());
    }

    [Fact]
    public async Task AddingTheSameFavoriteTwice_HasNoEffect()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        var recipe = (await GenerateAsync(alice, householdId)).Value!;

        await AddFavoriteAsync(alice, householdId, recipe.Id);
        var second = await AddFavoriteAsync(alice, householdId, recipe.Id);

        Assert.Equal(1, second.Value!.FavoriteCount);
    }

    [Fact]
    public async Task RemovingMyStar_KeepsTheOtherMembersStars()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        var bob = await CreateUserAsync("Bob");
        await AddMemberAsync(alice, householdId, bob);
        var recipe = (await GenerateAsync(alice, householdId)).Value!;
        await AddFavoriteAsync(alice, householdId, recipe.Id);
        await AddFavoriteAsync(bob, householdId, recipe.Id);

        var afterRemoval = await WithServiceAsync<IRecipeService, Result<RecipeDto>>(s =>
            s.RemoveFavoriteAsync(alice, householdId, recipe.Id, CancellationToken.None));

        Assert.False(afterRemoval.Value!.IsFavorite);
        Assert.Equal(1, afterRemoval.Value.FavoriteCount);
    }

    [Fact]
    public async Task TwoHundredFirstFavorite_IsRefused()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        var recipe = (await GenerateAsync(alice, householdId)).Value!;
        await using (var db = CreateDbContext())
        {
            var others = Enumerable.Range(0, RecipeService.MaxFavoritesPerUser)
                .Select(_ => new RecipeGeneration { HouseholdId = householdId, RequestedByUserId = alice, CreatedAt = Clock.Now, RecipeJson = "{}" })
                .ToList();
            db.RecipeGenerations.AddRange(others);
            db.RecipeFavorites.AddRange(others.Select(r => new RecipeFavorite { RecipeGenerationId = r.Id, UserId = alice, CreatedAt = Clock.Now }));
            await db.SaveChangesAsync();
        }

        var result = await AddFavoriteAsync(alice, householdId, recipe.Id);

        Assert.Equal(RecipeErrors.FavoriteLimitReached, result.Error);
    }

    [Fact]
    public async Task FavoriteOnAnotherHouseholdsRecipe_IsNotFound()
    {
        var (alice, aliceHousehold) = await HouseholdWithFridgeAsync();
        var mallory = await CreateUserAsync("Mallory");
        var malloryHousehold = await CreateHouseholdAsync(mallory, "Chez Mallory");
        var recipe = (await GenerateAsync(alice, aliceHousehold)).Value!;

        var result = await AddFavoriteAsync(mallory, malloryHousehold, recipe.Id);

        Assert.Equal(RecipeErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task LeavingTheHousehold_RemovesTheirStars()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        var bob = await CreateUserAsync("Bob");
        await AddMemberAsync(alice, householdId, bob);
        var recipe = (await GenerateAsync(alice, householdId)).Value!;
        await AddFavoriteAsync(bob, householdId, recipe.Id);

        await RemoveMemberAsync(bob, householdId, bob);

        Assert.Empty(await FavoritesAsync(alice, householdId));
    }

    [Fact]
    public async Task DeletingTheAuthorsAccount_KeepsTheRecipeInTheHouseholdCookbook()
    {
        var (alice, householdId) = await HouseholdWithFridgeAsync();
        var bob = await CreateUserAsync("Bob");
        await AddMemberAsync(alice, householdId, bob);
        var recipe = (await GenerateAsync(alice, householdId)).Value!;
        await AddFavoriteAsync(bob, householdId, recipe.Id);

        await DeleteAccountAsync(alice);

        Assert.Equal(recipe.Id, Assert.Single(await FavoritesAsync(bob, householdId)).Id);
        await using var db = CreateDbContext();
        Assert.Null((await db.RecipeGenerations.SingleAsync(r => r.Id == recipe.Id)).RequestedByUserId);
    }

    // ---------- Helpers ----------

    private Task<Result<RecipeDto>> AddFavoriteAsync(Guid userId, Guid householdId, Guid recipeId) =>
        WithServiceAsync<IRecipeService, Result<RecipeDto>>(s =>
            s.AddFavoriteAsync(userId, householdId, recipeId, CancellationToken.None));

    private Task<IReadOnlyList<RecipeDto>> FavoritesAsync(Guid userId, Guid householdId) =>
        WithServiceAsync<IRecipeService, IReadOnlyList<RecipeDto>>(s =>
            s.ListFavoritesAsync(userId, householdId, CancellationToken.None));

    private async Task<(Guid UserId, Guid HouseholdId)> HouseholdWithFridgeAsync()
    {
        var alice = await CreateUserAsync("Alice");
        var householdId = await CreateHouseholdAsync(alice);
        await AddItemAsync(alice, householdId, "Pâtes", "dry-goods", daysLeft: 300);
        await AddItemAsync(alice, householdId, "Steak haché", "ground-meat", daysLeft: 1);
        await AddItemAsync(alice, householdId, "Yaourt", "yogurts", daysLeft: 4);
        await AddItemAsync(alice, householdId, "Courgette", "vegetables", daysLeft: 3);
        return (alice, householdId);
    }

    private async Task<Guid> AddItemAsync(Guid userId, Guid householdId, string name, string category, int daysLeft, bool personal = false)
    {
        var categoryId = CategorySeed.Categories.Single(c => c.Code == category).Id;
        var result = await WithServiceAsync<IInventoryService, Result<InventoryItemDto>>(s => s.CreateAsync(
            userId, householdId,
            new SaveInventoryItemRequest(name, categoryId, 1, QuantityUnit.Piece, Today, Today.AddDays(daysLeft), null, personal),
            CancellationToken.None));
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value.Id;
    }

    private Task SaveProfileAsync(Guid userId, Diet diet, Allergen[] allergens) =>
        WithServiceAsync<IProfileService, Result<ProfileDto>>(s => s.SaveAsync(userId,
            new SaveProfileRequest(CookingTime.Under30Minutes, MealBudget.Under2Euros, diet, [], allergens,
                allergens.Length > 0, NutritionGoal.Balanced, 1),
            CancellationToken.None));

    private Task SaveTastesAsync(Guid userId, DislikedFood[] dislikes) =>
        WithServiceAsync<IProfileService, Result<ProfileDto>>(s => s.SaveAsync(userId,
            new SaveProfileRequest(CookingTime.Under30Minutes, MealBudget.Under2Euros, Diet.Omnivore, [], [],
                false, NutritionGoal.Balanced, 1, dislikes),
            CancellationToken.None));

    private async Task<Result<RecipeDto>> GenerateAsync(
        Guid userId, Guid householdId, GenerateRecipeRequest? request = null, AiOptions? options = null)
    {
        await using var scope = CreateScope();
        var service = options is null
            ? scope.ServiceProvider.GetRequiredService<IRecipeService>()
            : new RecipeService(scope.ServiceProvider.GetRequiredService<AppDbContext>(), Generator,
                Microsoft.Extensions.Options.Options.Create(options), Clock, NullLogger<RecipeService>.Instance);
        return await service.GenerateAsync(userId, householdId, request ?? new GenerateRecipeRequest(null, null), CancellationToken.None);
    }

    private Task<RecipeQuotaDto> QuotaAsync(Guid userId) =>
        WithServiceAsync<IRecipeService, RecipeQuotaDto>(s => s.GetQuotaAsync(userId, CancellationToken.None));

    private Task<int> PurgeAsync() =>
        WithServiceAsync<IRecipeService, int>(s => s.PurgeAsync(CancellationToken.None));

    private Task<Result<int>> MarkCookedAsync(Guid userId, Guid householdId, Guid recipeId, IReadOnlyList<Guid> itemIds) =>
        WithServiceAsync<IRecipeService, Result<int>>(s =>
            s.MarkCookedAsync(userId, householdId, recipeId, itemIds, CancellationToken.None));
}
