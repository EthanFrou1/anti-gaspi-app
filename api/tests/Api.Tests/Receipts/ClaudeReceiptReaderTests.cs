using System.Net;
using System.Text.Json;
using Api.Options;
using Api.Services.Ai;
using Api.Services.Receipts;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static Api.Tests.Infrastructure.FakeAnthropicServer;
using static Api.Tests.Receipts.ReceiptValidatorTests;

namespace Api.Tests.Receipts;

/// <summary>
/// Le vrai client Claude, branché sur un faux serveur Anthropic (voir ClaudeRecipeGeneratorTests
/// pour le traitement détaillé des erreurs, commun aux deux).
/// </summary>
public class ClaudeReceiptReaderTests
{
    private static readonly ReceiptPrompt Prompt = ReceiptPromptBuilder.Build(Categories, Today);

    private static readonly ReceiptImage Image = new([0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3], "image/jpeg");

    private const string ReceiptJson =
        """{"purchaseDate":"2026-09-23","lines":[{"receiptText":"COURGETTE","isFood":true,"name":"Courgette","category":"vegetables","quantity":0.5,"unit":"Kilogram"}]}""";

    [Fact]
    public async Task SendsTheImage_TheReceiptModel_AndTheImposedJsonFormat()
    {
        var (reader, server) = Create(_ => Message(ReceiptJson, "end_turn"));

        await reader.ReadAsync(Image, Prompt, CancellationToken.None);

        using var body = JsonDocument.Parse(server.LastBody!);
        var root = body.RootElement;
        Assert.Equal("claude-haiku-4-5-20251001", root.GetProperty("model").GetString());
        Assert.Equal(4000, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal("json_schema", root.GetProperty("output_config").GetProperty("format").GetProperty("type").GetString());

        var content = root.GetProperty("messages")[0].GetProperty("content");
        // L'image d'abord, en base64, puis la consigne.
        Assert.Equal("image", content[0].GetProperty("type").GetString());
        var source = content[0].GetProperty("source");
        Assert.Equal("base64", source.GetProperty("type").GetString());
        Assert.Equal("image/jpeg", source.GetProperty("media_type").GetString());
        Assert.Equal(Convert.ToBase64String(Image.Data), source.GetProperty("data").GetString());
        Assert.Equal(Prompt.UserContent, content[1].GetProperty("text").GetString());
    }

    [Fact]
    public async Task ReceiptModel_IsConfigurable()
    {
        var (reader, server) = Create(_ => Message(ReceiptJson, "end_turn"), new AiOptions { ReceiptModel = "claude-sonnet-5" });

        await reader.ReadAsync(Image, Prompt, CancellationToken.None);

        using var body = JsonDocument.Parse(server.LastBody!);
        Assert.Equal("claude-sonnet-5", body.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task ValidResponse_IsParsedIntoADraft()
    {
        var (reader, _) = Create(_ => Message(ReceiptJson, "end_turn"));

        var draft = await reader.ReadAsync(Image, Prompt, CancellationToken.None);

        Assert.Equal("2026-09-23", draft.PurchaseDate);
        var line = Assert.Single(draft.Lines!);
        Assert.Equal("Courgette", line.Name);
        Assert.Equal(0.5m, line.Quantity);
        Assert.Equal("Kilogram", line.Unit);
        Assert.True(line.IsFood);
    }

    [Fact]
    public async Task RefusalOrApiError_IsUnavailable()
    {
        var (refused, _) = Create(_ => Message(ReceiptJson, "refusal"));
        var (overloaded, _) = Create(_ => Error((HttpStatusCode)529, "overloaded_error"));

        await Assert.ThrowsAsync<AiUnavailableException>(() => refused.ReadAsync(Image, Prompt, CancellationToken.None));
        await Assert.ThrowsAsync<AiUnavailableException>(() => overloaded.ReadAsync(Image, Prompt, CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "invalid_request_error")]
    [InlineData((HttpStatusCode)529, "overloaded_error")]
    public async Task ApiErrors_AreLogged_WithoutTheImage(HttpStatusCode status, string errorType)
    {
        var logs = new CapturingLoggerProvider();
        using var factory = new LoggerFactory([logs]);
        var server = new FakeAnthropicServer(_ => Error(status, errorType));
        var reader = new ClaudeReceiptReader(
            server.CreateClient(), Microsoft.Extensions.Options.Options.Create(new AiOptions()), new Logger<ClaudeReceiptReader>(factory));
        var photo = TestJpeg.PhotoWithMetadata;

        await Assert.ThrowsAsync<AiUnavailableException>(() =>
            reader.ReadAsync(new ReceiptImage(photo, "image/jpeg"), Prompt, CancellationToken.None));

        // L'image est bien partie dans la requête… mais n'apparaît pas dans les journaux.
        using var body = JsonDocument.Parse(server.LastBody!);
        var sent = body.RootElement.GetProperty("messages")[0].GetProperty("content")[0].GetProperty("source").GetProperty("data");
        Assert.Equal(Convert.ToBase64String(photo), sent.GetString());
        Assert.NotEmpty(logs.Entries);
        Assert.DoesNotContain(Convert.ToBase64String(photo), logs.AllText);
        Assert.DoesNotContain(Convert.ToBase64String(photo[100..160]), logs.AllText);
    }

    private static (ClaudeReceiptReader Reader, FakeAnthropicServer Server) Create(
        Func<HttpRequestMessage, HttpResponseMessage> respond, AiOptions? options = null)
    {
        var server = new FakeAnthropicServer(respond);
        var reader = new ClaudeReceiptReader(
            server.CreateClient(),
            Microsoft.Extensions.Options.Options.Create(options ?? new AiOptions()),
            NullLogger<ClaudeReceiptReader>.Instance);
        return (reader, server);
    }
}
