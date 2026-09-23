using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Dtos.Auth;
using Api.Dtos.Profiles;
using Api.Dtos.Users;
using Api.Entities;
using Api.Services.Auth;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.EndToEnd;

[Collection(DatabaseCollection.Name)]
public class ProfileEndpointsTests(DatabaseFixture database) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Onboarding_FromNoProfileToSavedProfile()
    {
        using var client = await AuthenticatedClientAsync();

        var before = await client.GetAsync("/api/me/profile");
        var saved = await client.PutAsJsonAsync("/api/me/profile", new SaveProfileRequest(
            CookingTime.Under15Minutes, MealBudget.From2To4Euros, Diet.Vegan, [IngredientExclusion.Alcohol],
            [Allergen.Peanuts], HealthDataConsent: true, NutritionGoal.MuscleGain, DefaultServings: 2), Json);
        var me = await client.GetFromJsonAsync<UserDto>("/api/me", Json);

        Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);
        Assert.Equal("profile.none", (await before.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Contains("\"diet\":\"Vegan\"", await saved.Content.ReadAsStringAsync());
        Assert.True(me!.HasProfile);
    }

    [Fact]
    public async Task AllergiesWithoutConsent_Return400OnTheConsentField()
    {
        using var client = await AuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/me/profile", new SaveProfileRequest(
            CookingTime.Under30Minutes, MealBudget.Under2Euros, Diet.Omnivore, [], [Allergen.Milk],
            HealthDataConsent: false, NutritionGoal.Balanced, 1), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("HealthDataConsent", out _));
    }

    [Theory]
    // Valeur numérique : refusée, sinon « 99 » deviendrait une valeur d'énumération inexistante.
    [InlineData("99")]
    [InlineData("\"Carnivore\"")]
    public async Task UnknownEnumValue_Returns400(string dietJson)
    {
        using var client = await AuthenticatedClientAsync();
        var body = $$"""
            {"cookingTime":"Under30Minutes","budget":"Under2Euros","diet":{{dietJson}},"exclusions":[],"allergens":[],
             "healthDataConsent":false,"goal":"Balanced","defaultServings":1}
            """;

        var response = await client.PutAsync("/api/me/profile", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OutOfRangeServings_Returns400()
    {
        using var client = await AuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/me/profile", new SaveProfileRequest(
            CookingTime.Under30Minutes, MealBudget.Under2Euros, Diet.Omnivore, [], [], false, NutritionGoal.Balanced, 50), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WithoutToken_Returns401()
    {
        using var anonymous = database.Api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/me/profile")).StatusCode);
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        using var scope = database.Api.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAuthService>().RegisterAsync(
            new RegisterRequest($"profil-{Guid.NewGuid():N}@example.com", DatabaseTestBase.DefaultPassword, "Profil"),
            CancellationToken.None);

        var client = database.Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.Value!.AccessToken);
        return client;
    }
}
