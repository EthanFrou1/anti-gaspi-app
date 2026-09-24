namespace Api.Services.Receipts;

/// <summary>
/// Lecteur factice, déterministe et gratuit : il renvoie toujours le même petit ticket,
/// quelle que soit la photo (dont un article non alimentaire, que la validation écarte).
/// Utilisé par les tests et en développement ; interdit ailleurs (AiConfigurationGuard).
/// </summary>
public sealed class FakeReceiptReader : IReceiptReader
{
    public Task<ReceiptDraft> ReadAsync(ReceiptImage image, ReceiptPrompt prompt, CancellationToken ct)
    {
        IReadOnlyList<ReceiptDraftLine> lines =
        [
            new("STEAK HACHE 5%MG X2", "Steak haché 5 % MG", "ground-meat", 2, "Piece", IsFood: true),
            new("YAOURT NATURE X4", "Yaourt nature", "yogurts", 4, "Piece", IsFood: true),
            new("COURGETTE VRAC", "Courgette", "vegetables", 0.612m, "Kilogram", IsFood: true),
            new("PENNE RIGATE 500G", "Penne rigate", "dry-goods", 500, "Gram", IsFood: true),
            new("LAIT DEMI ECR 1L", "Lait demi-écrémé", "uht-milk", 1, "Liter", IsFood: true),
            new("LESSIVE LIQ 2L", "Lessive liquide", "other", 1, "Piece", IsFood: false),
        ];
        // Date absente : la validation retient la date du jour.
        return Task.FromResult(new ReceiptDraft(PurchaseDate: null, lines));
    }
}
