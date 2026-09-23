using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Dtos.Auth;
using Api.Dtos.Households;
using Api.Dtos.Inventory;
using Api.Entities;
using Api.Services.Auth;
using Api.Services.Households;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.EndToEnd;

/// <summary>
/// Tests de bout en bout : de vraies requêtes HTTP traversent toute l'API
/// (authentification, politiques d'autorisation, validation, JSON) jusqu'à PostgreSQL.
/// Ils complètent les tests des services, qui ne voient pas cette « plomberie ».
/// </summary>
[Collection(DatabaseCollection.Name)]
public class InventoryEndpointsTests(DatabaseFixture database) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateItem_Returns201_WithEstimatedExpiryAndLocation()
    {
        var alice = await CreateUserAsync("Alice");
        var householdId = await CreateHouseholdAsync(alice.Id);

        var response = await alice.Client.PostAsJsonAsync(ItemsUrl(householdId), NewItem("Steak haché", categoryId: 1), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var item = await response.Content.ReadFromJsonAsync<InventoryItemDto>(Json);
        Assert.Equal(Today.AddDays(1), item!.ExpiresOn);
        Assert.True(item.ExpiryIsEstimated);
        // Les enums circulent en texte, comme l'attend l'app mobile.
        Assert.Contains("\"unit\":\"Piece\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DecimalQuantity_IsAccepted_WhenTheServerCultureIsFrench()
    {
        // Régression : en culture fr-FR, la validation de la quantité provoquait une erreur 500.
        var previousCulture = CultureInfo.CurrentCulture;
        var previousDefault = CultureInfo.DefaultThreadCurrentCulture;
        var french = new CultureInfo("fr-FR");
        CultureInfo.CurrentCulture = french;
        CultureInfo.DefaultThreadCurrentCulture = french;
        try
        {
            var alice = await CreateUserAsync("Alice");
            var householdId = await CreateHouseholdAsync(alice.Id);

            var response = await alice.Client.PostAsJsonAsync(
                ItemsUrl(householdId), NewItem("Farine", categoryId: 23, quantity: 0.5m, unit: QuantityUnit.Kilogram), Json);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(0.5m, (await response.Content.ReadFromJsonAsync<InventoryItemDto>(Json))!.Quantity);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.DefaultThreadCurrentCulture = previousDefault;
        }
    }

    [Fact]
    public async Task InvalidItem_Returns400_WithFieldErrorsInFrench()
    {
        var alice = await CreateUserAsync("Alice");
        var householdId = await CreateHouseholdAsync(alice.Id);

        var response = await alice.Client.PostAsJsonAsync(
            ItemsUrl(householdId), NewItem("", categoryId: 1, quantity: 0) with { Barcode = "abc" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadProblemAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Name", out _));
        Assert.True(errors.TryGetProperty("Quantity", out _));
        Assert.True(errors.TryGetProperty("Barcode", out _));
    }

    [Fact]
    public async Task WithoutToken_Returns401()
    {
        var alice = await CreateUserAsync("Alice");
        var householdId = await CreateHouseholdAsync(alice.Id);
        using var anonymous = database.Api.CreateClient();

        var response = await anonymous.GetAsync(ItemsUrl(householdId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WithTamperedToken_Returns401()
    {
        var alice = await CreateUserAsync("Alice");
        var householdId = await CreateHouseholdAsync(alice.Id);
        alice.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", alice.Client.DefaultRequestHeaders.Authorization!.Parameter + "x");

        var response = await alice.Client.GetAsync(ItemsUrl(householdId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PersonalItemOfAnotherMember_Returns403()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var householdId = await CreateHouseholdAsync(alice.Id);
        await AddMemberAsync(alice.Id, householdId, bob.Id);
        var created = await alice.Client.PostAsJsonAsync(ItemsUrl(householdId), NewItem("Mon fromage", 11) with { IsPersonal = true }, Json);
        var item = await created.Content.ReadFromJsonAsync<InventoryItemDto>(Json);

        var response = await bob.Client.PostAsync($"{ItemsUrl(householdId)}/{item!.Id}/consume", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("inventory.not_owner", (await ReadProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task HouseholdOfSomeoneElse_Returns404_WithoutRevealingThatItExists()
    {
        var alice = await CreateUserAsync("Alice");
        var mallory = await CreateUserAsync("Mallory");
        var householdId = await CreateHouseholdAsync(alice.Id);

        var response = await mallory.Client.GetAsync(ItemsUrl(householdId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("household.not_found", (await ReadProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task UnknownItem_Returns404()
    {
        var alice = await CreateUserAsync("Alice");
        var householdId = await CreateHouseholdAsync(alice.Id);

        var response = await alice.Client.GetAsync($"{ItemsUrl(householdId)}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("inventory.item_not_found", (await ReadProblemAsync(response)).GetProperty("code").GetString());
    }

    // ---------- Helpers ----------

    private sealed record TestUser(Guid Id, HttpClient Client);

    private static string ItemsUrl(Guid householdId) => $"/api/households/{householdId}/items";

    private static SaveInventoryItemRequest NewItem(
        string name, int categoryId, decimal quantity = 1, QuantityUnit unit = QuantityUnit.Piece) =>
        new(name, categoryId, quantity, unit, Today, ExpiresOn: null, Barcode: null, IsPersonal: false);

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Json));

    // Comptes créés par le service (pas par /api/auth/register) : le rate limiting de
    // l'authentification bloquerait sinon une série de tests.
    private async Task<TestUser> CreateUserAsync(string name)
    {
        using var scope = database.Api.Services.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var result = await auth.RegisterAsync(
            new RegisterRequest($"{name.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com", DatabaseTestBase.DefaultPassword, name),
            CancellationToken.None);

        var client = database.Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.Value!.AccessToken);
        return new TestUser(result.Value.User.Id, client);
    }

    private async Task<Guid> CreateHouseholdAsync(Guid ownerId)
    {
        using var scope = database.Api.Services.CreateScope();
        var households = scope.ServiceProvider.GetRequiredService<IHouseholdService>();
        var result = await households.CreateAsync(ownerId, new CreateHouseholdRequest("Coloc"), CancellationToken.None);
        return result.Value!.Id;
    }

    private async Task AddMemberAsync(Guid ownerId, Guid householdId, Guid memberId)
    {
        using var scope = database.Api.Services.CreateScope();
        var invitation = await scope.ServiceProvider.GetRequiredService<IInvitationService>()
            .CreateAsync(ownerId, householdId, CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<IHouseholdService>()
            .JoinAsync(memberId, invitation.Value!.Code, CancellationToken.None);
    }
}
