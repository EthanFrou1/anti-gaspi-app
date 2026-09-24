using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Api.Options;
using Api.Services.Ai;
using Microsoft.Extensions.Options;

namespace Api.Services.Receipts;

/// <summary>
/// Lecture d'un ticket par Claude (vision). L'image est envoyée en base64 dans la requête,
/// puis oubliée : elle n'est ni enregistrée ni journalisée.
/// </summary>
public sealed class ClaudeReceiptReader(
    AnthropicClient client,
    IOptions<AiOptions> options,
    ILogger<ClaudeReceiptReader> logger) : IReceiptReader
{
    public Task<ReceiptDraft> ReadAsync(ReceiptImage image, ReceiptPrompt prompt, CancellationToken ct)
    {
        var settings = options.Value;
        var parameters = new MessageCreateParams
        {
            Model = settings.ReceiptModel,
            MaxTokens = settings.ReceiptMaxOutputTokens,
            System = prompt.System,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    // L'image d'abord, puis la consigne : l'ordre conseillé pour la vision.
                    Content = new List<ContentBlockParam>
                    {
                        new ImageBlockParam
                        {
                            Source = new Base64ImageSource
                            {
                                Data = Convert.ToBase64String(image.Data),
                                MediaType = image.MediaType,
                            },
                        },
                        new TextBlockParam { Text = prompt.UserContent },
                    },
                },
            ],
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat { Schema = new Dictionary<string, JsonElement>(prompt.OutputSchema) },
            },
        };

        return ClaudeStructuredCall.SendAsync<ReceiptDraft>(client, parameters, logger, "Lecture de ticket", ct);
    }
}
