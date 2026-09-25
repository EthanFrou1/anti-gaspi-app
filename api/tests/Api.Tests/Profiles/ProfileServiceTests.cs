using Api.Common;
using Api.Dtos.Households;
using Api.Dtos.Profiles;
using Api.Dtos.Users;
using Api.Entities;
using Api.Services.Households;
using Api.Services.Profiles;
using Api.Services.Users;
using Api.Tests.Households;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Profiles;

public class ProfileServiceTests(DatabaseFixture database) : HouseholdTestBase(database)
{
    private static SaveProfileRequest Request(
        Diet diet = Diet.Omnivore,
        Allergen[]? allergens = null,
        bool consent = false,
        IngredientExclusion[]? exclusions = null,
        int servings = 1) =>
        new(CookingTime.Under30Minutes, MealBudget.Under2Euros, diet, exclusions ?? [], allergens ?? [], consent,
            NutritionGoal.SimpleAntiWaste, servings);

    // ---------- Onboarding ----------

    [Fact]
    public async Task NewUser_HasNoProfile()
    {
        var alice = await CreateUserAsync("Alice");

        Assert.Equal(ProfileErrors.NoProfile, (await GetAsync(alice)).Error);
        Assert.False((await GetUserAsync(alice)).HasProfile);
    }

    [Fact]
    public async Task Save_CreatesTheProfile_AndMarksOnboardingAsDone()
    {
        var alice = await CreateUserAsync("Alice");

        var saved = await SaveAsync(alice, Request(diet: Diet.Vegetarian, exclusions: [IngredientExclusion.Alcohol], servings: 2));

        Assert.True(saved.IsSuccess);
        Assert.Equal(Diet.Vegetarian, saved.Value.Diet);
        Assert.Equal([IngredientExclusion.Alcohol], saved.Value.Exclusions);
        Assert.True((await GetUserAsync(alice)).HasProfile);
        Assert.Equal(2, (await GetAsync(alice)).Value!.DefaultServings);
    }

    [Fact]
    public async Task Save_Twice_ReplacesTheProfile()
    {
        var alice = await CreateUserAsync("Alice");
        await SaveAsync(alice, Request(diet: Diet.Vegan));

        await SaveAsync(alice, Request(diet: Diet.Pescatarian));

        Assert.Equal(Diet.Pescatarian, (await GetAsync(alice)).Value!.Diet);
        await using var db = CreateDbContext();
        Assert.Equal(1, await db.UserProfiles.CountAsync(p => p.UserId == alice));
    }

    [Fact]
    public async Task Save_RemovesDuplicatesAndSortsLists()
    {
        var alice = await CreateUserAsync("Alice");

        var saved = await SaveAsync(alice, Request(
            exclusions: [IngredientExclusion.Pork, IngredientExclusion.Alcohol, IngredientExclusion.Pork]));

        Assert.Equal([IngredientExclusion.Pork, IngredientExclusion.Alcohol], saved.Value!.Exclusions);
    }

    [Fact]
    public async Task Tastes_AreSavedWithoutConsent_AndOptional()
    {
        var alice = await CreateUserAsync("Alice");

        // Des goûts, pas des données de santé : aucun consentement demandé.
        var saved = await SaveAsync(alice, Request() with
        {
            Dislikes = [DislikedFood.Olives, DislikedFood.Mushrooms, DislikedFood.Olives],
            AvoidSpicy = true,
        });
        // Champs facultatifs : absents, le profil n'a aucun goût.
        var withoutTastes = await SaveAsync(await CreateUserAsync("Bob"), Request());

        Assert.Equal([DislikedFood.Mushrooms, DislikedFood.Olives], saved.Value!.Dislikes);
        Assert.True(saved.Value.AvoidSpicy);
        Assert.Empty(withoutTastes.Value!.Dislikes);
        Assert.False(withoutTastes.Value.AvoidSpicy);
    }

    [Fact]
    public async Task UnknownDislikedFood_IsRejected()
    {
        var alice = await CreateUserAsync("Alice");

        var result = await SaveAsync(alice, Request() with { Dislikes = [(DislikedFood)999] });

        Assert.Equal(ErrorType.Validation, result.Error?.Type);
    }

    // ---------- Allergies : donnée de santé (RGPD, article 9) ----------

    [Fact]
    public async Task Allergies_WithoutConsent_AreRejected()
    {
        var alice = await CreateUserAsync("Alice");

        var result = await SaveAsync(alice, Request(allergens: [Allergen.Peanuts], consent: false));

        Assert.Equal(ProfileErrors.ConsentRequired, result.Error);
        Assert.Equal(ProfileErrors.NoProfile, (await GetAsync(alice)).Error);
    }

    [Fact]
    public async Task Allergies_WithConsent_AreStoredWithTheConsentDate()
    {
        var alice = await CreateUserAsync("Alice");

        var saved = await SaveAsync(alice, Request(allergens: [Allergen.Peanuts, Allergen.Sesame], consent: true));

        Assert.Equal([Allergen.Peanuts, Allergen.Sesame], saved.Value!.Allergens);
        Assert.True(saved.Value.HealthDataConsent);
        await using var db = CreateDbContext();
        var consentAt = (await db.UserProfiles.SingleAsync(p => p.UserId == alice)).HealthDataConsentAt;
        Assert.NotNull(consentAt);
        Assert.Equal(Clock.Now, consentAt.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task ConsentDate_IsTheFirstOne_AndIsNotRewrittenOnLaterSaves()
    {
        var alice = await CreateUserAsync("Alice");
        await SaveAsync(alice, Request(allergens: [Allergen.Milk], consent: true));
        var firstConsent = Clock.Now;

        Clock.Advance(TimeSpan.FromDays(10));
        await SaveAsync(alice, Request(allergens: [Allergen.Milk, Allergen.Eggs], consent: true));

        await using var db = CreateDbContext();
        var consentAt = (await db.UserProfiles.SingleAsync(p => p.UserId == alice)).HealthDataConsentAt;
        Assert.Equal(firstConsent, consentAt!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task WithdrawingConsent_ErasesAllergies()
    {
        var alice = await CreateUserAsync("Alice");
        await SaveAsync(alice, Request(allergens: [Allergen.Gluten], consent: true));

        var saved = await SaveAsync(alice, Request(allergens: [], consent: false));

        Assert.Empty(saved.Value!.Allergens);
        Assert.False(saved.Value.HealthDataConsent);
        await using var db = CreateDbContext();
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == alice);
        Assert.Empty(profile.Allergens);
        Assert.Null(profile.HealthDataConsentAt);
    }

    [Fact]
    public async Task Profile_IsDeletedWithTheAccount()
    {
        var alice = await CreateUserAsync("Alice");
        await SaveAsync(alice, Request(allergens: [Allergen.Fish], consent: true));

        await DeleteAccountAsync(alice);

        await using var db = CreateDbContext();
        Assert.False(await db.UserProfiles.AnyAsync(p => p.UserId == alice));
    }

    [Fact]
    public async Task UndefinedEnumValue_IsRejected()
    {
        var alice = await CreateUserAsync("Alice");

        var result = await SaveAsync(alice, Request() with { Diet = (Diet)99 });

        Assert.Equal(ErrorType.Validation, result.Error?.Type);
    }

    // ---------- Équipement du foyer ----------

    [Fact]
    public async Task NewHousehold_HasHobAndMicrowaveByDefault()
    {
        var alice = await CreateUserAsync("Alice");
        await CreateHouseholdAsync(alice);

        var household = await WithServiceAsync<IHouseholdService, Result<HouseholdDto>>(s =>
            s.GetMineAsync(alice, CancellationToken.None));

        Assert.Equal([KitchenEquipment.Hob, KitchenEquipment.Microwave], household.Value!.Equipment);
    }

    [Fact]
    public async Task AnyMember_CanUpdateTheSharedKitchenEquipment()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var householdId = await CreateHouseholdAsync(alice);
        await AddMemberAsync(alice, householdId, bob);

        var updated = await WithServiceAsync<IHouseholdService, Result<HouseholdDto>>(s => s.UpdateEquipmentAsync(
            bob, householdId, [KitchenEquipment.Oven, KitchenEquipment.AirFryer, KitchenEquipment.Oven], CancellationToken.None));

        Assert.Equal([KitchenEquipment.Oven, KitchenEquipment.AirFryer], updated.Value!.Equipment);
        var seenByAlice = await WithServiceAsync<IHouseholdService, Result<HouseholdDto>>(s =>
            s.GetMineAsync(alice, CancellationToken.None));
        Assert.Equal(updated.Value.Equipment, seenByAlice.Value!.Equipment);
    }

    // ---------- Helpers ----------

    private Task<Result<ProfileDto>> SaveAsync(Guid userId, SaveProfileRequest request) =>
        WithServiceAsync<IProfileService, Result<ProfileDto>>(s => s.SaveAsync(userId, request, CancellationToken.None));

    private Task<Result<ProfileDto>> GetAsync(Guid userId) =>
        WithServiceAsync<IProfileService, Result<ProfileDto>>(s => s.GetAsync(userId, CancellationToken.None));

    private async Task<UserDto> GetUserAsync(Guid userId) =>
        (await WithServiceAsync<IUserService, Result<UserDto>>(s => s.GetAsync(userId, CancellationToken.None))).Value!;
}
