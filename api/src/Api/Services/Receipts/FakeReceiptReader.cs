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
            new("STEAK HACHE 5%MG X2", "Steak haché 5 % MG", "ground-meat", 1, "Piece", IsFood: true, LinePrice: 3.49m, PackSize: 2),
            new("YAOURT NATURE X4", "Yaourt nature", "yogurts", 1, "Piece", IsFood: true, LinePrice: 1.45m, PackSize: 4),
            new("COURGETTE VRAC", "Courgette", "vegetables", 0.612m, "Kilogram", IsFood: true, LinePrice: 1.52m),
            new("PENNE RIGATE 500G", "Penne rigate", "dry-goods", 500, "Gram", IsFood: true, LinePrice: 0.89m),
            // Acheté en plusieurs exemplaires, prix cohérents (6 × 0,99 = 5,94) : l'écran de
            // validation affiche « 6 × 1 l ».
            new("LAIT DEMI ECR 1L", "Lait demi-écrémé", "uht-milk", 1, "Liter", IsFood: true,
                Copies: 6, LinePrice: 5.94m, UnitPrice: 0.99m),
            new("LESSIVE LIQ 2L", "Lessive liquide", "other", 1, "Piece", IsFood: false, LinePrice: 7.95m),
        ];
        // Date absente : la validation retient la date du jour.
        return Task.FromResult(new ReceiptDraft(PurchaseDate: null, lines));
    }
}
