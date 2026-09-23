using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Data.Seed;
using Api.Dtos.Auth;
using Api.Dtos.Households;
using Api.Dtos.Inventory;
using Api.Dtos.Recipes;
using Api.Entities;
using Api.Services.Auth;
using Api.Services.Households;
using Api.Services.Inventory;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.EndToEnd;

[Collection(DatabaseCollection.Name)]
public class RecipesEndpointsTests(DatabaseFixture database) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Generate_Returns201_ThenTheRecipeIsInTheHistory()
    {
        var (client, householdId) = await SetupAsync(database.Api);

        var response = await client.PostAsJsonAsync(RecipesUrl(householdId), new GenerateRecipeRequest(null, null), Json);
        var history = await client.GetFromJsonAsync<List<RecipeDto>>(RecipesUrl(householdId), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>(Json);
        Assert.Contains(recipe!.Ingredients, i => i.InventoryItemId is not null);
        Assert.Equal(recipe.Id, Assert.Single(history!).Id);
    }

    [Fact]
    public async Task FourthGeneration_Returns429_WithQuotaCode()
    {
        var (client, householdId) = await SetupAsync(database.Api);
        for (var i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync(RecipesUrl(householdId), new GenerateRecipeRequest(null, null), Json);
        }

        var fourth = await client.PostAsJsonAsync(RecipesUrl(householdId), new GenerateRecipeRequest(null, null), Json);
        var quota = await client.GetFromJsonAsync<RecipeQuotaDto>("/api/me/recipe-quota", Json);

        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
        Assert.Equal("recipe.daily_quota", (await fourth.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(0, quota!.Remaining);
    }

    [Fact]
    public async Task MarkCooked_ConsumesTheCheckedProducts()
    {
        var (client, householdId) = await SetupAsync(database.Api);
        var created = await client.PostAsJsonAsync(RecipesUrl(householdId), new GenerateRecipeRequest(null, null), Json);
        var recipe = await created.Content.ReadFromJsonAsync<RecipeDto>(Json);
        var finished = recipe!.Ingredients.Select(i => i.InventoryItemId).OfType<Guid>().ToList();

        var response = await client.PostAsJsonAsync(
            $"{RecipesUrl(householdId)}/{recipe.Id}/cooked", new MarkRecipeCookedRequest(finished), Json);
        var fridge = await client.GetFromJsonAsync<List<InventoryItemDto>>($"/api/households/{householdId}/items", Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(fridge!, i => finished.Contains(i.Id));
    }

    [Fact]
    public async Task Favorite_PutThenListThenDelete()
    {
        var (client, householdId) = await SetupAsync(database.Api);
        var created = await client.PostAsJsonAsync(RecipesUrl(householdId), new GenerateRecipeRequest(null, null), Json);
        var recipe = await created.Content.ReadFromJsonAsync<RecipeDto>(Json);

        var put = await client.PutAsync($"{RecipesUrl(householdId)}/{recipe!.Id}/favorite", null);
        var favorites = await client.GetFromJsonAsync<List<RecipeDto>>($"{RecipesUrl(householdId)}/favorites", Json);
        var delete = await client.DeleteAsync($"{RecipesUrl(householdId)}/{recipe.Id}/favorite");
        var afterDelete = await client.GetFromJsonAsync<List<RecipeDto>>($"{RecipesUrl(householdId)}/favorites", Json);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.True(Assert.Single(favorites!).IsFavorite);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Empty(afterDelete!);
    }

    [Fact]
    public async Task PromptPreview_DoesNotExistOutsideDevelopment()
    {
        var (client, householdId) = await SetupAsync(database.Api);
        using var anonymous = database.Api.CreateClient();

        var asMember = await client.PostAsJsonAsync($"{RecipesUrl(householdId)}/prompt-preview", new GenerateRecipeRequest(null, null), Json);
        var asAnonymous = await anonymous.PostAsJsonAsync($"{RecipesUrl(householdId)}/prompt-preview", new GenerateRecipeRequest(null, null), Json);

        // 404 même sans authentification : la route n'existe tout simplement pas.
        Assert.Equal(HttpStatusCode.NotFound, asMember.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, asAnonymous.StatusCode);
    }

    [Fact]
    public async Task PromptPreview_WorksInDevelopment_WithoutUsingTheQuota()
    {
        await using var devApi = new DevelopmentApiFactory(database.ConnectionString);
        var (client, householdId) = await SetupAsync(devApi);

        var response = await client.PostAsJsonAsync($"{RecipesUrl(householdId)}/prompt-preview", new GenerateRecipeRequest(null, null), Json);
        var quota = await client.GetFromJsonAsync<RecipeQuotaDto>("/api/me/recipe-quota", Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<RecipePromptPreviewDto>(Json);
        Assert.Contains("Steak haché", preview!.UserContent);
        Assert.Contains("FORMAT DE RÉPONSE IMPOSÉ", preview.ClipboardText);
        Assert.Equal(0, quota!.Used);
    }

    // ---------- Helpers ----------

    private static string RecipesUrl(Guid householdId) => $"/api/households/{householdId}/recipes";

    private static async Task<(HttpClient Client, Guid HouseholdId)> SetupAsync(WebApplicationFactory<Program> api)
    {
        using var scope = api.Services.CreateScope();
        var services = scope.ServiceProvider;
        var user = await services.GetRequiredService<IAuthService>().RegisterAsync(
            new RegisterRequest($"chef-{Guid.NewGuid():N}@example.com", DatabaseTestBase.DefaultPassword, "Chef"),
            CancellationToken.None);
        var userId = user.Value!.User.Id;
        var household = await services.GetRequiredService<IHouseholdService>()
            .CreateAsync(userId, new CreateHouseholdRequest("Coloc"), CancellationToken.None);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var steak = CategorySeed.Categories.Single(c => c.Code == "ground-meat").Id;
        var pasta = CategorySeed.Categories.Single(c => c.Code == "dry-goods").Id;
        var inventory = services.GetRequiredService<IInventoryService>();
        await inventory.CreateAsync(userId, household.Value!.Id,
            new SaveInventoryItemRequest("Steak haché", steak, 2, QuantityUnit.Piece, today, null, null, false), CancellationToken.None);
        await inventory.CreateAsync(userId, household.Value.Id,
            new SaveInventoryItemRequest("Pâtes", pasta, 500, QuantityUnit.Gram, today, null, null, false), CancellationToken.None);

        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Value.AccessToken);
        return (client, household.Value.Id);
    }

    /// <summary>
    /// API en environnement Development (générateur Fake, outils de mise au point présents),
    /// branchée sur la base de test.
    /// </summary>
    private sealed class DevelopmentApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Default", connectionString);
            builder.UseSetting("Jwt:SigningKey", DatabaseTestBase.TestJwtOptions.SigningKey);
            // Toujours Fake ici, même si une vraie clé est présente dans les user-secrets.
            builder.UseSetting("Ai:Provider", "Fake");
        }
    }
}
