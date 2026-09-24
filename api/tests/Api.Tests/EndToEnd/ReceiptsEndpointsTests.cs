using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Data.Seed;
using Api.Dtos.Auth;
using Api.Dtos.Households;
using Api.Dtos.Inventory;
using Api.Dtos.Receipts;
using Api.Entities;
using Api.Services.Auth;
using Api.Services.Households;
using Api.Services.Receipts;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using static Api.Tests.Receipts.ReceiptServiceTests;

namespace Api.Tests.EndToEnd;

[Collection(DatabaseCollection.Name)]
public class ReceiptsEndpointsTests(DatabaseFixture database) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- Lecture ----------

    [Fact]
    public async Task Scan_ThenAddTheValidatedLines_PutsThemInTheFridge()
    {
        var (client, householdId) = await SetupAsync();

        var response = await client.PostAsync(ScanUrl(householdId), ImageForm(Jpeg));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scan = await response.Content.ReadFromJsonAsync<ReceiptScanDto>(Json);

        // L'utilisateur garde deux lignes et corrige un nom, comme sur l'écran de validation.
        var kept = scan!.Lines.Take(2)
            .Select((l, i) => new SaveInventoryItemRequest(
                i == 0 ? "Steak haché" : l.Name, l.CategoryId, l.Quantity, l.Unit, scan.PurchasedOn, null, null, false))
            .ToList();
        var added = await client.PostAsJsonAsync(BatchUrl(householdId), new CreateInventoryItemsRequest(kept), Json);
        var fridge = await client.GetFromJsonAsync<List<InventoryItemDto>>(ItemsUrl(householdId), Json);

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Equal(2, fridge!.Count);
        Assert.Contains(fridge, i => i.Name == "Steak haché" && i.ExpiresOn == scan.Lines[0].ExpiresOn);
    }

    [Fact]
    public async Task Scan_ByANonMember_Returns404()
    {
        var (_, householdId) = await SetupAsync();
        var (outsider, _) = await SetupAsync();

        var response = await outsider.PostAsync(ScanUrl(householdId), ImageForm(Jpeg));

        // 404 et non 403 : on ne révèle pas qu'un foyer existe à cette adresse.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Scan_WithoutImage_OrWithAnotherFormat_Returns400()
    {
        var (client, householdId) = await SetupAsync();

        var missing = await client.PostAsync(ScanUrl(householdId), new MultipartFormDataContent());
        var gif = await client.PostAsync(ScanUrl(householdId), ImageForm([0x47, 0x49, 0x46, 0x38, 0x39, 0x61], "image/jpeg"));

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, gif.StatusCode);
        // Le type annoncé (image/jpeg) ne suffit pas : ce sont les octets qui comptent.
        Assert.Equal("receipt.invalid_image", (await gif.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Scan_WithATooLargeBody_IsRejected()
    {
        var (client, householdId) = await SetupAsync();
        var huge = new byte[ReceiptImageFormat.MaxBytes + 200 * 1024];
        Jpeg.CopyTo(huge, 0);

        var response = await client.PostAsync(ScanUrl(householdId), ImageForm(huge));

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge,
            $"Statut inattendu : {response.StatusCode}");
    }

    [Fact]
    public async Task FourthScan_Returns429_AndTheQuotaEndpointSaysSo()
    {
        var (client, householdId) = await SetupAsync();
        for (var i = 0; i < 3; i++)
        {
            await client.PostAsync(ScanUrl(householdId), ImageForm(Jpeg));
        }

        var fourth = await client.PostAsync(ScanUrl(householdId), ImageForm(Jpeg));
        var quota = await client.GetFromJsonAsync<ReceiptQuotaDto>("/api/me/receipt-quota", Json);

        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
        Assert.Equal("receipt.daily_quota", (await fourth.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(0, quota!.Remaining);
    }

    // ---------- Ajout groupé ----------

    [Fact]
    public async Task Batch_WithAnInvalidLine_Returns400_WithTheLineIndex_AndAddsNothing()
    {
        var (client, householdId) = await SetupAsync();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var request = new CreateInventoryItemsRequest(
        [
            new SaveInventoryItemRequest("Pâtes", CategoryId("dry-goods"), 500, QuantityUnit.Gram, today, null, null, false),
            new SaveInventoryItemRequest("", CategoryId("dry-goods"), 1, QuantityUnit.Piece, today, null, null, false),
        ]);

        var response = await client.PostAsJsonAsync(BatchUrl(householdId), request, Json);
        var fridge = await client.GetFromJsonAsync<List<InventoryItemDto>>(ItemsUrl(householdId), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Items[1].Name", out _), errors.ToString());
        Assert.Empty(fridge!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public async Task Batch_WithNoItem_OrTooMany_Returns400(int count)
    {
        var (client, householdId) = await SetupAsync();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var items = Enumerable.Range(0, count)
            .Select(i => new SaveInventoryItemRequest($"Produit {i}", CategoryId("other"), 1, QuantityUnit.Piece, today, null, null, false))
            .ToList();

        var response = await client.PostAsJsonAsync(BatchUrl(householdId), new CreateInventoryItemsRequest(items), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Helpers ----------

    private static int CategoryId(string code) => CategorySeed.Categories.Single(c => c.Code == code).Id;

    private static string ScanUrl(Guid householdId) => $"/api/households/{householdId}/receipts/scan";

    private static string ItemsUrl(Guid householdId) => $"/api/households/{householdId}/items";

    private static string BatchUrl(Guid householdId) => $"{ItemsUrl(householdId)}/batch";

    private static MultipartFormDataContent ImageForm(byte[] image, string contentType = "image/jpeg")
    {
        var file = new ByteArrayContent(image);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "image", "ticket.jpg" } };
    }

    private async Task<(HttpClient Client, Guid HouseholdId)> SetupAsync()
    {
        using var scope = database.Api.Services.CreateScope();
        var services = scope.ServiceProvider;
        var user = await services.GetRequiredService<IAuthService>().RegisterAsync(
            new RegisterRequest($"coloc-{Guid.NewGuid():N}@example.com", DatabaseTestBase.DefaultPassword, "Coloc"),
            CancellationToken.None);
        var household = await services.GetRequiredService<IHouseholdService>()
            .CreateAsync(user.Value!.User.Id, new CreateHouseholdRequest("Coloc"), CancellationToken.None);

        var client = database.Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Value.AccessToken);
        return (client, household.Value!.Id);
    }
}
