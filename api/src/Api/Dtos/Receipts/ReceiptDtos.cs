using Api.Entities;

namespace Api.Dtos.Receipts;

/// <summary>
/// Résultat d'une lecture de ticket, pour l'écran de validation. Rien n'est enregistré
/// dans le frigo : l'app renvoie ensuite les lignes gardées à l'ajout groupé.
/// </summary>
public sealed record ReceiptScanDto(
    DateOnly PurchasedOn,
    // false : date absente du ticket ou invraisemblable, remplacée par aujourd'hui.
    bool PurchaseDateFromReceipt,
    IReadOnlyList<ReceiptLineDto> Lines,
    // Articles non alimentaires ou illisibles, écartés.
    int SkippedLineCount);

public sealed record ReceiptLineDto(
    // Libellé brut du ticket : aide l'utilisateur à reconnaître la ligne.
    string ReceiptText,
    string Name,
    int CategoryId,
    // Quantité totale, celle qui va dans le frigo.
    decimal Quantity,
    QuantityUnit Unit,
    // Détail lu sur le ticket (« 6 × 1 l ») : Quantity = Copies × QuantityPerCopy.
    int Copies,
    decimal QuantityPerCopy,
    // Date estimée à partir de la catégorie et de la date d'achat.
    DateOnly ExpiresOn,
    ExpiryKind ExpiryKind);

public sealed record ReceiptQuotaDto(int Used, int Limit, int Remaining, DateTimeOffset ResetsAt);
