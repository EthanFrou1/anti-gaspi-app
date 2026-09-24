using Api.Services.Ai;

namespace Api.Services.Receipts;

/// <summary>
/// Utilisé en développement quand Ai:Provider = Claude mais que la clé Anthropic n'est pas
/// renseignée : la lecture répond 503 « IA non configurée » (voir UnconfiguredRecipeGenerator).
/// </summary>
public sealed class UnconfiguredReceiptReader : IReceiptReader
{
    public Task<ReceiptDraft> ReadAsync(ReceiptImage image, ReceiptPrompt prompt, CancellationToken ct) =>
        throw new AiUnavailableException(
            "IA non configurée : renseigne la clé Anthropic:ApiKey dans les user-secrets.");
}
