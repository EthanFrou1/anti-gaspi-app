using Api.Common;
using Api.Data.Seed;
using Api.Dtos.Inventory;
using Api.Entities;
using Api.Services.Inventory;
using Api.Tests.Households;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Inventory;

public class InventoryServiceTests(DatabaseFixture database) : HouseholdTestBase(database)
{
    private static int CategoryId(string code) => CategorySeed.Categories.Single(c => c.Code == code).Id;

    private DateOnly Today => DateOnly.FromDateTime(Clock.Now.UtcDateTime);

    private SaveInventoryItemRequest Request(
        string name = "Yaourts nature",
        string category = "yogurts",
        DateOnly? purchasedOn = null,
        DateOnly? expiresOn = null,
        bool isPersonal = false,
        decimal quantity = 4,
        QuantityUnit unit = QuantityUnit.Piece,
        string? barcode = null) =>
        new(name, CategoryId(category), quantity, unit, purchasedOn ?? Today, expiresOn, barcode, isPersonal);

    // ---------- Création ----------

    [Fact]
    public async Task Create_WithoutExpiryDate_EstimatesItFromTheCategory()
    {
        var (alice, householdId) = await CreateHouseholdAsync();

        var result = await CreateAsync(alice, householdId, Request(category: "ground-meat"));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(Today.AddDays(1), result.Value.ExpiresOn);
        Assert.True(result.Value.ExpiryIsEstimated);
        Assert.Equal(ExpiryKind.UseBy, result.Value.ExpiryKind);
    }

    [Fact]
    public async Task Create_WithExpiryDate_KeepsTheUserDate()
    {
        var (alice, householdId) = await CreateHouseholdAsync();
        var printedDate = Today.AddDays(12);

        var result = await CreateAsync(alice, householdId, Request(expiresOn: printedDate));

        Assert.Equal(printedDate, result.Value!.ExpiresOn);
        Assert.False(result.Value.ExpiryIsEstimated);
    }

    [Fact]
    public async Task Create_AlreadyExpiredProduct_IsAllowed()
    {
        // Rayon anti-gaspi : produit vendu à prix réduit, date déjà dépassée.
        var (alice, householdId) = await CreateHouseholdAsync();

        var result = await CreateAsync(alice, householdId, Request(category: "dry-goods", expiresOn: Today.AddDays(-10)));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Create_PersonalItem_BelongsToItsCreator()
    {
        var (alice, householdId) = await CreateHouseholdAsync();

        var personal = (await CreateAsync(alice, householdId, Request(isPersonal: true))).Value!;
        var common = (await CreateAsync(alice, householdId, Request(isPersonal: false))).Value!;

        Assert.Equal(alice, personal.OwnerUserId);
        Assert.Equal("Alice", personal.OwnerDisplayName);
        Assert.Null(common.OwnerUserId);
    }

    [Fact]
    public async Task Create_WithUnknownCategory_ReturnsValidationErrorOnCategory()
    {
        var (alice, householdId) = await CreateHouseholdAsync();

        var result = await CreateAsync(alice, householdId, Request() with { CategoryId = 999 });

        Assert.Equal(InventoryErrors.UnknownCategory, result.Error);
    }

    [Theory]
    [InlineData(2, null)]      // achat dans le futur (au-delà de demain)
    [InlineData(-2000, null)]  // achat il y a plus de 5 ans
    [InlineData(0, 4000)]      // péremption plus de 10 ans après l'achat
    [InlineData(0, -400)]      // péremption plus d'un an avant l'achat
    public async Task Create_WithOutOfRangeDates_IsRejected(int purchaseOffsetDays, int? expiryOffsetDays)
    {
        var (alice, householdId) = await CreateHouseholdAsync();
        var purchasedOn = Today.AddDays(purchaseOffsetDays);
        DateOnly? expiresOn = expiryOffsetDays is null ? null : purchasedOn.AddDays(expiryOffsetDays.Value);

        var result = await CreateAsync(alice, householdId, Request(purchasedOn: purchasedOn, expiresOn: expiresOn));

        Assert.Equal(ErrorType.Validation, result.Error?.Type);
    }

    [Fact]
    public async Task Create_BeyondTheHouseholdLimit_IsRejected()
    {
        var (alice, householdId) = await CreateHouseholdAsync();
        await using (var db = CreateDbContext())
        {
            db.InventoryItems.AddRange(Enumerable.Range(0, InventoryService.MaxActiveItemsPerHousehold).Select(i => new InventoryItem
            {
                HouseholdId = householdId,
                Name = $"Produit {i}",
                CategoryId = CategoryId("other"),
                Quantity = 1,
                PurchasedOn = Today,
                ExpiresOn = Today.AddDays(7),
            }));
            await db.SaveChangesAsync();
        }

        var result = await CreateAsync(alice, householdId, Request());

        Assert.Equal(InventoryErrors.LimitReached, result.Error);
    }

    // ---------- Ajout groupé (ticket de caisse) ----------

    [Fact]
    public async Task CreateMany_AddsAllItems_SortedByExpiry()
    {
        var (alice, householdId) = await CreateHouseholdAsync();

        var result = await CreateManyAsync(alice, householdId,
            Request("Pâtes", "dry-goods"), Request("Steak haché", "ground-meat", isPersonal: true), Request("Yaourt", "yogurts"));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(["Steak haché", "Yaourt", "Pâtes"], result.Value.Select(i => i.Name));
        Assert.Equal(alice, result.Value[0].OwnerUserId);
        Assert.All(result.Value, i => Assert.True(i.ExpiryIsEstimated));
        Assert.Equal(3, (await ListAsync(householdId)).Count);
    }

    [Fact]
    public async Task CreateMany_WithOneInvalidLine_AddsNothing_AndNamesTheLine()
    {
        var (alice, householdId) = await CreateHouseholdAsync();
        var invalid = Request("Mystère") with { CategoryId = 999 };

        var result = await CreateManyAsync(alice, householdId, Request("Pâtes", "dry-goods"), invalid);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal(["Items[1].CategoryId"], result.Error.ValidationErrors!.Keys);
        // Tout ou rien : les pâtes, pourtant valides, n'ont pas été ajoutées.
        Assert.Empty(await ListAsync(householdId));
    }

    [Fact]
    public async Task CreateMany_BeyondTheHouseholdLimit_AddsNothing()
    {
        var (alice, householdId) = await CreateHouseholdAsync();
        await using (var db = CreateDbContext())
        {
            db.InventoryItems.AddRange(Enumerable.Range(0, InventoryService.MaxActiveItemsPerHousehold - 1).Select(i => new InventoryItem
            {
                HouseholdId = householdId,
                Name = $"Produit {i}",
                CategoryId = CategoryId("other"),
                Quantity = 1,
                PurchasedOn = Today,
                ExpiresOn = Today.AddDays(7),
            }));
            await db.SaveChangesAsync();
        }

        // Une place restante, deux produits : refusé en bloc.
        var result = await CreateManyAsync(alice, householdId, Request("A"), Request("B"));

        Assert.Equal(InventoryErrors.LimitReached, result.Error);
        Assert.Equal(InventoryService.MaxActiveItemsPerHousehold - 1, (await ListAsync(householdId)).Count);
    }

    // ---------- Consultation ----------

    [Fact]
    public async Task List_SortsByExpiryDateAndHidesConsumedItems()
    {
        var (alice, householdId) = await CreateHouseholdAsync();
        await CreateAsync(alice, householdId, Request(name: "Pâtes", category: "dry-goods"));
        await CreateAsync(alice, householdId, Request(name: "Steak haché", category: "ground-meat"));
        var eaten = (await CreateAsync(alice, householdId, Request(name: "Yaourt mangé"))).Value!;
        await ChangeStatusAsync(alice, householdId, eaten.Id, InventoryItemStatus.Consumed);

        var active = await ListAsync(householdId);
        var consumed = await ListAsync(householdId, InventoryItemStatus.Consumed);

        Assert.Equal(["Steak haché", "Pâtes"], active.Select(i => i.Name));
        Assert.Equal("Yaourt mangé", Assert.Single(consumed).Name);
    }

    [Fact]
    public async Task List_NeverReturnsItemsOfAnotherHousehold()
    {
        var (alice, aliceHousehold) = await CreateHouseholdAsync();
        var (mallory, malloryHousehold) = await CreateHouseholdAsync("Mallory");
        await CreateAsync(alice, aliceHousehold, Request(name: "Chez Alice"));
        await CreateAsync(mallory, malloryHousehold, Request(name: "Chez Mallory"));

        var items = await ListAsync(malloryHousehold);

        Assert.Equal("Chez Mallory", Assert.Single(items).Name);
    }

    // ---------- Modification ----------

    [Fact]
    public async Task Update_CommonItem_IsAllowedForAnyMember()
    {
        var (alice, bob, householdId) = await CreateHouseholdWithMemberAsync();
        var item = (await CreateAsync(alice, householdId, Request())).Value!;

        var result = await UpdateAsync(bob, householdId, item.Id, Request(name: "Yaourts à la vanille", quantity: 2));

        Assert.True(result.IsSuccess);
        Assert.Equal("Yaourts à la vanille", result.Value.Name);
        Assert.Equal(2, result.Value.Quantity);
    }

    [Fact]
    public async Task Update_WithoutExpiryDate_ReEstimatesFromTheNewCategory()
    {
        var (alice, householdId) = await CreateHouseholdAsync();
        var item = (await CreateAsync(alice, householdId, Request(category: "yogurts"))).Value!;

        var result = await UpdateAsync(alice, householdId, item.Id, Request(category: "fresh-cheese"));

        Assert.Equal(Today.AddDays(7), result.Value!.ExpiresOn);
        Assert.True(result.Value.ExpiryIsEstimated);
    }

    [Fact]
    public async Task Update_ItemOfAnotherHousehold_ReturnsNotFound()
    {
        var (alice, aliceHousehold) = await CreateHouseholdAsync();
        var (mallory, malloryHousehold) = await CreateHouseholdAsync("Mallory");
        var item = (await CreateAsync(alice, aliceHousehold, Request())).Value!;

        // Mallory passe son propre foyer dans l'URL et l'identifiant du produit d'Alice.
        var result = await UpdateAsync(mallory, malloryHousehold, item.Id, Request(name: "Piraté"));

        Assert.Equal(InventoryErrors.ItemNotFound, result.Error);
        Assert.Equal("Yaourts nature", (await GetAsync(aliceHousehold, item.Id)).Value!.Name);
    }

    [Fact]
    public async Task Update_ConsumedItem_IsRejected()
    {
        var (alice, householdId) = await CreateHouseholdAsync();
        var item = (await CreateAsync(alice, householdId, Request())).Value!;
        await ChangeStatusAsync(alice, householdId, item.Id, InventoryItemStatus.Consumed);

        var result = await UpdateAsync(alice, householdId, item.Id, Request(name: "Trop tard"));

        Assert.Equal(InventoryErrors.NotActive, result.Error);
    }

    // ---------- Produits perso ----------

    [Fact]
    public async Task PersonalItem_CannotBeModifiedByAnotherMember()
    {
        var (alice, bob, householdId) = await CreateHouseholdWithMemberAsync();
        var item = (await CreateAsync(alice, householdId, Request(isPersonal: true))).Value!;

        Assert.Equal(InventoryErrors.NotOwner, (await UpdateAsync(bob, householdId, item.Id, Request())).Error);
        Assert.Equal(InventoryErrors.NotOwner, (await ChangeStatusAsync(bob, householdId, item.Id, InventoryItemStatus.Consumed)).Error);
        Assert.Equal(InventoryErrors.NotOwner, (await DeleteAsync(bob, householdId, item.Id)).Error);
    }

    [Fact]
    public async Task PersonalItem_IsVisibleToTheWholeHousehold()
    {
        var (alice, bob, householdId) = await CreateHouseholdWithMemberAsync();
        await CreateAsync(alice, householdId, Request(isPersonal: true));

        var itemsSeenByBob = await ListAsync(householdId);

        Assert.Equal("Alice", Assert.Single(itemsSeenByBob).OwnerDisplayName);
    }

    [Fact]
    public async Task PersonalItems_BecomeCommon_WhenTheirOwnerLeaves()
    {
        var (alice, bob, householdId) = await CreateHouseholdWithMemberAsync();
        var item = (await CreateAsync(bob, householdId, Request(isPersonal: true))).Value!;

        await RemoveMemberAsync(bob, householdId, bob);

        Assert.Null((await GetAsync(householdId, item.Id)).Value!.OwnerUserId);
    }

    [Fact]
    public async Task PersonalItems_BecomeCommon_WhenTheirOwnerDeletesTheirAccount()
    {
        var (alice, bob, householdId) = await CreateHouseholdWithMemberAsync();
        var item = (await CreateAsync(bob, householdId, Request(isPersonal: true))).Value!;

        await DeleteAccountAsync(bob);

        Assert.Null((await GetAsync(householdId, item.Id)).Value!.OwnerUserId);
    }

    [Fact]
    public async Task Items_AreDeleted_WithTheHousehold()
    {
        var (alice, householdId) = await CreateHouseholdAsync();
        await CreateAsync(alice, householdId, Request());

        await RemoveMemberAsync(alice, householdId, alice);

        await using var db = CreateDbContext();
        Assert.False(await db.InventoryItems.AnyAsync(i => i.HouseholdId == householdId));
    }

    // ---------- Statuts et suppression ----------

    [Theory]
    [InlineData(InventoryItemStatus.Consumed)]
    [InlineData(InventoryItemStatus.Discarded)]
    public async Task ChangeStatus_RecordsTheStatusAndItsDate(InventoryItemStatus status)
    {
        var (alice, householdId) = await CreateHouseholdAsync();
        var item = (await CreateAsync(alice, householdId, Request())).Value!;
        Clock.Advance(TimeSpan.FromHours(3));

        var result = await ChangeStatusAsync(alice, householdId, item.Id, status);

        Assert.Equal(status, result.Value!.Status);
        // PostgreSQL stocke les dates à la microseconde (.NET : 0,1 µs) : tolérance nécessaire.
        Assert.NotNull(result.Value.StatusChangedAt);
        Assert.Equal(Clock.Now, result.Value.StatusChangedAt.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task ChangeStatus_Twice_IsRejected()
    {
        var (alice, householdId) = await CreateHouseholdAsync();
        var item = (await CreateAsync(alice, householdId, Request())).Value!;
        await ChangeStatusAsync(alice, householdId, item.Id, InventoryItemStatus.Consumed);

        var result = await ChangeStatusAsync(alice, householdId, item.Id, InventoryItemStatus.Discarded);

        Assert.Equal(InventoryErrors.NotActive, result.Error);
    }

    // ---------- Consommation partielle ----------

    [Theory]
    [InlineData(InventoryItemStatus.Consumed)]
    [InlineData(InventoryItemStatus.Discarded)]
    public async Task ChangeStatus_OfAPart_KeepsTheRestInTheFridge_AndRecordsThePart(InventoryItemStatus status)
    {
        var (alice, householdId) = await CreateHouseholdAsync();
        var cheese = (await CreateAsync(alice, householdId,
            Request("Emmental râpé", "hard-cheese", quantity: 600, unit: QuantityUnit.Gram, isPersonal: true))).Value!;
        Clock.Advance(TimeSpan.FromHours(3));

        var result = await ChangeStatusAsync(alice, householdId, cheese.Id, status, quantity: 200);

        // Le produit reste au frigo (même Id : rappels et recettes toujours valables), avec le reste.
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(cheese.Id, result.Value.Id);
        Assert.Equal(InventoryItemStatus.Active, result.Value.Status);
        Assert.Equal(400, result.Value.Quantity);

        // La part mangée ou jetée devient une ligne d'historique, copie du produit (compteur de gaspillage).
        var part = Assert.Single(await ListAsync(householdId, status));
        Assert.NotEqual(cheese.Id, part.Id);
        Assert.Equal(200, part.Quantity);
        Assert.Equal(QuantityUnit.Gram, part.Unit);
        Assert.Equal((cheese.Name, cheese.CategoryId, cheese.ExpiresOn, cheese.OwnerUserId), (part.Name, part.CategoryId, part.ExpiresOn, part.OwnerUserId));
        Assert.Equal(Clock.Now, part.StatusChangedAt!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task ChangeStatus_OfTheWholeQuantity_ChangesTheItemItself()
    {
        var (alice, householdId) = await CreateHouseholdAsync();
        var item = (await CreateAsync(alice, householdId, Request(quantity: 2))).Value!;

        var result = await ChangeStatusAsync(alice, householdId, item.Id, InventoryItemStatus.Consumed, quantity: 2);

        // Pas de ligne en plus : c'est le produit entier.
        Assert.Equal(InventoryItemStatus.Consumed, result.Value!.Status);
        Assert.Empty(await ListAsync(householdId));
        Assert.Equal(item.Id, Assert.Single(await ListAsync(householdId, InventoryItemStatus.Consumed)).Id);
    }

    [Fact]
    public async Task ChangeStatus_OfMoreThanWhatIsLeft_IsRejected()
    {
        var (alice, householdId) = await CreateHouseholdAsync();
        var item = (await CreateAsync(alice, householdId, Request(quantity: 2))).Value!;

        var result = await ChangeStatusAsync(alice, householdId, item.Id, InventoryItemStatus.Consumed, quantity: 3);

        Assert.Equal(InventoryErrors.QuantityExceedsStock, result.Error);
        Assert.Equal(2, (await GetAsync(householdId, item.Id)).Value!.Quantity);
    }

    [Fact]
    public async Task ChangeStatus_OfAPart_Twice_LeavesTheRightQuantity()
    {
        var (alice, householdId) = await CreateHouseholdAsync();
        var item = (await CreateAsync(alice, householdId, Request(quantity: 4))).Value!;

        await ChangeStatusAsync(alice, householdId, item.Id, InventoryItemStatus.Consumed, quantity: 1);
        await ChangeStatusAsync(alice, householdId, item.Id, InventoryItemStatus.Discarded, quantity: 1);

        Assert.Equal(2, (await GetAsync(householdId, item.Id)).Value!.Quantity);
        Assert.Single(await ListAsync(householdId, InventoryItemStatus.Consumed));
        Assert.Single(await ListAsync(householdId, InventoryItemStatus.Discarded));
    }

    [Fact]
    public async Task ChangeStatus_OfAPart_OfAPersonalItemOfAnotherMember_IsRejected()
    {
        var (alice, bob, householdId) = await CreateHouseholdWithMemberAsync();
        var item = (await CreateAsync(alice, householdId, Request(quantity: 4, isPersonal: true))).Value!;

        var result = await ChangeStatusAsync(bob, householdId, item.Id, InventoryItemStatus.Consumed, quantity: 1);

        Assert.Equal(InventoryErrors.NotOwner, result.Error);
        Assert.Equal(4, (await GetAsync(householdId, item.Id)).Value!.Quantity);
    }

    [Fact]
    public async Task Delete_RemovesTheItem()
    {
        var (alice, householdId) = await CreateHouseholdAsync();
        var item = (await CreateAsync(alice, householdId, Request())).Value!;

        var result = await DeleteAsync(alice, householdId, item.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(InventoryErrors.ItemNotFound, (await GetAsync(householdId, item.Id)).Error);
    }

    // ---------- Helpers ----------

    private async Task<(Guid UserId, Guid HouseholdId)> CreateHouseholdAsync(string name = "Alice")
    {
        var userId = await CreateUserAsync(name);
        return (userId, await CreateHouseholdAsync(userId));
    }

    private async Task<(Guid Owner, Guid Member, Guid HouseholdId)> CreateHouseholdWithMemberAsync()
    {
        var (alice, householdId) = await CreateHouseholdAsync();
        var bob = await CreateUserAsync("Bob");
        await AddMemberAsync(alice, householdId, bob);
        return (alice, bob, householdId);
    }

    private Task<Result<InventoryItemDto>> CreateAsync(Guid actorId, Guid householdId, SaveInventoryItemRequest request) =>
        WithServiceAsync<IInventoryService, Result<InventoryItemDto>>(s =>
            s.CreateAsync(actorId, householdId, request, CancellationToken.None));

    private Task<Result<IReadOnlyList<InventoryItemDto>>> CreateManyAsync(
        Guid actorId, Guid householdId, params SaveInventoryItemRequest[] requests) =>
        WithServiceAsync<IInventoryService, Result<IReadOnlyList<InventoryItemDto>>>(s =>
            s.CreateManyAsync(actorId, householdId, requests, CancellationToken.None));

    private Task<Result<InventoryItemDto>> UpdateAsync(
        Guid actorId, Guid householdId, Guid itemId, SaveInventoryItemRequest request) =>
        WithServiceAsync<IInventoryService, Result<InventoryItemDto>>(s =>
            s.UpdateAsync(actorId, householdId, itemId, request, CancellationToken.None));

    private Task<Result<InventoryItemDto>> ChangeStatusAsync(
        Guid actorId, Guid householdId, Guid itemId, InventoryItemStatus status, decimal? quantity = null) =>
        WithServiceAsync<IInventoryService, Result<InventoryItemDto>>(s =>
            s.ChangeStatusAsync(actorId, householdId, itemId, status, quantity, CancellationToken.None));

    private Task<Result> DeleteAsync(Guid actorId, Guid householdId, Guid itemId) =>
        WithServiceAsync<IInventoryService, Result>(s =>
            s.DeleteAsync(actorId, householdId, itemId, CancellationToken.None));

    private Task<Result<InventoryItemDto>> GetAsync(Guid householdId, Guid itemId) =>
        WithServiceAsync<IInventoryService, Result<InventoryItemDto>>(s =>
            s.GetAsync(householdId, itemId, CancellationToken.None));

    private Task<IReadOnlyList<InventoryItemDto>> ListAsync(
        Guid householdId, InventoryItemStatus status = InventoryItemStatus.Active) =>
        WithServiceAsync<IInventoryService, IReadOnlyList<InventoryItemDto>>(s =>
            s.ListAsync(householdId, status, CancellationToken.None));
}
