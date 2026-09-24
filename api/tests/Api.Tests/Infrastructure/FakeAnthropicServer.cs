using System.Net;
using System.Text;
using System.Text.Json;
using Anthropic;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Faux serveur Anthropic : le vrai client du SDK lui envoie ses requêtes. On vérifie ce qui
/// serait parti vers l'API et on simule chaque type de réponse, sans clé ni crédits consommés.
/// </summary>
public sealed class FakeAnthropicServer(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public string? LastBody { get; private set; }

    public Uri? LastUri { get; private set; }

    public AnthropicClient CreateClient() => new()
    {
        ApiKey = "cle-factice",
        HttpClient = new HttpClient(this),
        // Pas de nouvelle tentative : les tests restent rapides et déterministes.
        MaxRetries = 0,
    };

    /// <summary>Réponse « message » de l'API contenant le texte donné.</summary>
    public static HttpResponseMessage Message(string text, string stopReason)
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

    /// <summary>Erreur de l'API (429, 529, 401…).</summary>
    public static HttpResponseMessage Error(HttpStatusCode status, string errorType) => new(status)
    {
        Content = new StringContent(
            $$$"""{"type":"error","error":{"type":"{{{errorType}}}","message":"simulé"}}""", Encoding.UTF8, "application/json"),
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastUri = request.RequestUri;
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        return respond(request);
    }
}
