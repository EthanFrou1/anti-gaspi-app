using System.Net;
using System.Text;
using System.Text.Json;
using Anthropic;
using Api.Entities;
using Api.Options;
using Api.Services.Recipes;
using Microsoft.Extensions.Logging.Abstractions;
using static Api.Tests.Recipes.RecipePromptBuilderTests;

namespace Api.Tests.Recipes;

/// <summary>
/// Le vrai client Claude, branché sur un faux serveur Anthropic : on vérifie la requête
/// envoyée et le traitement de chaque type de réponse, sans clé ni crédits consommés.
/// </summary>
public class ClaudeRecipeGeneratorTests
{
    private static readonly RecipePrompt Prompt = RecipePromptBuilder.Build(
        Constraints(time: CookingTime.Under30Minutes), [Item("Courgette", "vegetables", 1)], Today);

    private const string RecipeJson =
        """{"title":"Courgette sautée","prepMinutes":15,"servings":2,"ingredients":[{"name":"Courgette","quantity":"1","inventoryRef":"p1"}],"steps":["Couper.","Cuire."]}""";

    [Fact]
    public async Task SendsTheConfiguredModel_TheImposedJsonFormat_AndTheCacheableSystemPrompt()
    {
        var (generator, server) = Create(_ => Message(RecipeJson, "end_turn"));

        await generator.GenerateAsync(Prompt, CancellationToken.None);

        using var body = JsonDocument.Parse(server.LastBody!);
        var root = body.RootElement;
        Assert.Equal("claude-haiku-4-5-20251001", root.GetProperty("model").GetString());
        Assert.Equal(1500, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal("json_schema", root.GetProperty("output_config").GetProperty("format").GetProperty("type").GetString());
        Assert.Equal("ephemeral", root.GetProperty("system")[0].GetProperty("cache_control").GetProperty("type").GetString());
        Assert.Equal(RecipePromptBuilder.SystemPrompt, root.GetProperty("system")[0].GetProperty("text").GetString());
        Assert.EndsWith("/v1/messages", server.LastUri!.AbsolutePath);
    }

    [Fact]
    public async Task ValidResponse_IsParsedIntoADraft()
    {
        var (generator, _) = Create(_ => Message(RecipeJson, "end_turn"));

        var draft = await generator.GenerateAsync(Prompt, CancellationToken.None);

        Assert.Equal("Courgette sautée", draft.Title);
        Assert.Equal("p1", draft.Ingredients![0].InventoryRef);
    }

    [Theory]
    [InlineData("refusal")]      // décision du projet : un refus est une erreur, sans repli
    [InlineData("max_tokens")]   // réponse coupée : JSON incomplet
    public async Task RefusalOrTruncatedAnswer_IsUnavailable(string stopReason)
    {
        // Même avec un JSON valide, un refus ou une réponse coupée ne doit pas être utilisé.
        var (generator, _) = Create(_ => Message(RecipeJson, stopReason));

        await Assert.ThrowsAsync<RecipeGenerationUnavailableException>(() =>
            generator.GenerateAsync(Prompt, CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limit_error")]
    [InlineData((HttpStatusCode)529, "overloaded_error")]
    [InlineData(HttpStatusCode.Unauthorized, "authentication_error")]
    public async Task ApiErrors_AreUnavailable(HttpStatusCode status, string errorType)
    {
        var (generator, _) = Create(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(
                $$$"""{"type":"error","error":{"type":"{{{errorType}}}","message":"simulé"}}""", Encoding.UTF8, "application/json"),
        });

        await Assert.ThrowsAsync<RecipeGenerationUnavailableException>(() =>
            generator.GenerateAsync(Prompt, CancellationToken.None));
    }

    [Fact]
    public async Task NetworkFailure_IsUnavailable()
    {
        var (generator, _) = Create(_ => throw new HttpRequestException("connexion refusée"));

        await Assert.ThrowsAsync<RecipeGenerationUnavailableException>(() =>
            generator.GenerateAsync(Prompt, CancellationToken.None));
    }

    // ---------- Faux serveur Anthropic ----------

    private static HttpResponseMessage Message(string text, string stopReason)
    {
        var body = JsonSerializer.Serialize(new
        {
            id = "msg_test",
            type = "message",
            role = "assistant",
            model = "claude-haiku-4-5-20251001",
            content = new[] { new { type = "text", text } },
            stop_reason = stopReason,
            stop_sequence = (string?)null,
            usage = new { input_tokens = 900, output_tokens = 250, cache_creation_input_tokens = 0, cache_read_input_tokens = 0 },
        });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private static (ClaudeRecipeGenerator Generator, FakeServer Server) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var server = new FakeServer(respond);
        var client = new AnthropicClient
        {
            ApiKey = "cle-factice",
            HttpClient = new HttpClient(server),
            // Pas de nouvelle tentative : les tests restent rapides et déterministes.
            MaxRetries = 0,
        };
        var generator = new ClaudeRecipeGenerator(
            client, Microsoft.Extensions.Options.Options.Create(new AiOptions()), NullLogger<ClaudeRecipeGenerator>.Instance);
        return (generator, server);
    }

    private sealed class FakeServer(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public string? LastBody { get; private set; }

        public Uri? LastUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastUri = request.RequestUri;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return respond(request);
        }
    }
}
