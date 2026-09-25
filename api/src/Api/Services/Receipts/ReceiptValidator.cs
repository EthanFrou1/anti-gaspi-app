using System.Globalization;
using Api.Common;
using Api.Dtos.Inventory;
using Api.Entities;
using Api.Services.Ai;
using Api.Services.Inventory;

namespace Api.Services.Receipts;

/// <summary>
/// Ligne de ticket prête pour l'écran de validation : catégorie connue, quantité bornée,
/// date de péremption estimée. Rien n'est encore enregistré dans le frigo.
/// </summary>
/// <param name="Quantity">Quantité totale (Copies × QuantityPerCopy) : celle qui va dans le frigo.</param>
public sealed record ValidatedReceiptLine(
    string ReceiptText,
    string Name,
    int CategoryId,
    decimal Quantity,
    QuantityUnit Unit,
    int Copies,
    decimal QuantityPerCopy,
    DateOnly ExpiresOn,
    ExpiryKind ExpiryKind);

public sealed record ValidatedReceipt(
    DateOnly PurchasedOn,
    // false : date absente du ticket ou invraisemblable, remplacée par aujourd'hui.
    bool PurchaseDateFromReceipt,
    IReadOnlyList<ValidatedReceiptLine> Lines,
    // Articles non alimentaires, illisibles ou au-delà de la limite : l'app l'indique.
    int SkippedLineCount);

/// <summary>
/// Vérifie la réponse de l'IA AVANT de l'utiliser (règle du projet). Fonction pure.
/// Contrairement aux recettes, une ligne douteuse est corrigée ou écartée, pas toute la
/// lecture : l'utilisateur relit de toute façon chaque ligne sur l'écran de validation.
/// Seule une réponse inexploitable dans son ensemble lève AiUnavailableException.
/// </summary>
public static class ReceiptValidator
{
    public const int MaxLines = 60;
    // Au-delà, la réponse n'a pas de sens pour un ticket (le prompt en demande 60 au plus).
    public const int MaxLinesRead = 100;
    public const int MaxNameLength = 100;
    public const int MaxReceiptTextLength = 60;
    // Un ticket plus ancien est sans doute une date mal lue : on retient aujourd'hui.
    public const int MaxReceiptAgeDays = 30;

    public const string FallbackCategoryCode = "other";

    // Mêmes bornes que la saisie d'un produit (SaveInventoryItemRequest).
    private const decimal MinQuantity = 0.001m;
    private const decimal MaxQuantity = 100_000m;
    // Au-delà, sans doute un nombre mal lu (prix, code) plutôt qu'un achat (exemplaires et lot).
    public const int MaxCopies = 99;
    // Écart accepté entre exemplaires × prix unitaire et prix de la ligne (arrondis du ticket).
    private const decimal PriceTolerance = 0.02m;
    private const decimal PriceToleranceRate = 0.01m;

    /// <summary>Unité acceptée en lecture seulement : convertie en millilitres par le code.</summary>
    public const string CentiliterUnit = "Centiliter";

    /// <summary>Unités que le modèle peut renvoyer (liste fermée du format de sortie).</summary>
    public static IReadOnlyList<string> ReadableUnits { get; } = [.. Enum.GetNames<QuantityUnit>(), CentiliterUnit];

    public static ValidatedReceipt Validate(ReceiptDraft draft, IReadOnlyList<CategoryDto> categories, DateOnly today)
    {
        var lines = draft.Lines
            ?? throw new AiUnavailableException("Réponse de l'IA invalide : liste d'articles absente.");
        if (lines.Count > MaxLinesRead)
        {
            throw new AiUnavailableException($"Réponse de l'IA invalide : {lines.Count} articles.");
        }

        var byCode = categories.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var fallback = byCode.GetValueOrDefault(FallbackCategoryCode)
            ?? throw new InvalidOperationException($"Catégorie « {FallbackCategoryCode} » absente de la base.");

        var (purchasedOn, fromReceipt) = PurchaseDate(draft.PurchaseDate, today);

        var validated = new List<ValidatedReceiptLine>();
        foreach (var line in lines)
        {
            var name = TextSanitizer.SingleLine(line.Name, MaxNameLength);
            if (!line.IsFood || name.Length == 0 || validated.Count == MaxLines)
            {
                continue;
            }

            var category = line.Category is not null && byCode.TryGetValue(line.Category, out var found) ? found : fallback;
            var quantity = Quantities(line);
            validated.Add(new ValidatedReceiptLine(
                TextSanitizer.SingleLine(line.ReceiptText, MaxReceiptTextLength),
                name,
                category.Id,
                quantity.Total,
                quantity.Unit,
                quantity.Copies,
                quantity.PerCopy,
                ExpiryEstimator.Estimate(purchasedOn, category.DefaultShelfLifeDays),
                category.ExpiryKind));
        }

        return new ValidatedReceipt(purchasedOn, fromReceipt, validated, lines.Count - validated.Count);
    }

    /// <summary>
    /// Date lue sur le ticket, si elle est vraisemblable : ni dans le futur, ni trop ancienne.
    /// </summary>
    public static (DateOnly Date, bool FromReceipt) PurchaseDate(string? value, DateOnly today)
    {
        var valid = DateOnly.TryParseExact(value?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            && date <= today
            && date >= today.AddDays(-MaxReceiptAgeDays);
        return valid ? (date, true) : (today, false);
    }

    /// <summary>
    /// Quantité totale = exemplaires × lot × contenu d'une unité. Le modèle recopie les nombres
    /// imprimés, le code fait les calculs : le modèle lit bien « 6 x » et « 1L », mais ne les
    /// multiplie ni ne les convertit de façon fiable (évaluation de design/test-tickets).
    /// Le total garde l'unité du contenu (3 × 400 g = 1 200 g). Hors bornes : un exemplaire.
    /// </summary>
    public static (int Copies, decimal PerCopy, decimal Total, QuantityUnit Unit) Quantities(ReceiptDraftLine line)
    {
        var (content, unit) = Content(line.Quantity, line.Unit);
        var pack = Count(line.PackSize);
        // En pièces, lot et quantité décrivent la même chose (« X4 » : lot de 4 × 1 pièce, ou
        // 1 × 4 pièces) : on retient le lot s'il est lu, sans les multiplier (sinon 16).
        var perCopy = unit == QuantityUnit.Piece
            ? (pack > 1 ? pack : content)
            : Math.Round(pack * content, 3, MidpointRounding.AwayFromZero);
        if (perCopy > MaxQuantity)
        {
            perCopy = content;
        }

        var copies = Copies(line.Copies, line.UnitPrice, line.LinePrice);
        var total = Math.Round(copies * perCopy, 3, MidpointRounding.AwayFromZero);
        return total > MaxQuantity ? (1, perCopy, perCopy, unit) : (copies, perCopy, total, unit);
    }

    /// <summary>
    /// Nombre d'exemplaires, vérifié par les prix : un multiplicateur est retenu seulement si
    /// exemplaires × prix unitaire ≈ prix de la ligne. Sinon, un rapport entier entre les deux
    /// prix le remplace (le modèle a mal lu le multiplicateur mais bien les prix). Sans prix
    /// unitaire lisible : 1. Un code TVA pris pour un multiplicateur (« 4,50 € 2 ») est ainsi
    /// écarté : aucun prix unitaire ne l'accompagne. En cas de doute, l'article compte une fois.
    /// </summary>
    public static int Copies(decimal? copies, decimal? unitPrice, decimal? linePrice)
    {
        if (unitPrice is not > 0 || linePrice is not > 0)
        {
            return 1;
        }

        var read = Count(copies);
        if (PricesMatch(read, unitPrice.Value, linePrice.Value))
        {
            return read;
        }

        var ratio = Math.Round(linePrice.Value / unitPrice.Value, MidpointRounding.AwayFromZero);
        return ratio >= 2 && ratio <= MaxCopies && PricesMatch((int)ratio, unitPrice.Value, linePrice.Value) ? (int)ratio : 1;
    }

    private static bool PricesMatch(int copies, decimal unitPrice, decimal linePrice) =>
        Math.Abs(copies * unitPrice - linePrice) <= Math.Max(PriceTolerance, linePrice * PriceToleranceRate);

    /// <summary>
    /// Nombre d'exemplaires ou taille d'un lot : un entier de 1 à MaxCopies. Absent, décimal
    /// ou hors bornes : 1 (l'utilisateur corrige sur l'écran de validation).
    /// </summary>
    public static int Count(decimal? value) =>
        value is { } v && v == decimal.Truncate(v) && v >= 1 && v <= MaxCopies ? (int)v : 1;

    /// <summary>
    /// Contenu d'une unité du lot. Les centilitres, lus tels qu'imprimés (« 20CL »), sont
    /// convertis en millilitres ici plutôt que par le modèle.
    /// </summary>
    public static (decimal Quantity, QuantityUnit Unit) Content(decimal quantity, string? unit) =>
        unit == CentiliterUnit ? Quantity(quantity * 10, nameof(QuantityUnit.Milliliter)) : Quantity(quantity, unit);

    /// <summary>
    /// Contenu d'un exemplaire. Unité inconnue ou quantité hors bornes : 1 pièce, que
    /// l'utilisateur corrigera.
    /// </summary>
    public static (decimal Quantity, QuantityUnit Unit) Quantity(decimal quantity, string? unit)
    {
        var rounded = Math.Round(quantity, 3, MidpointRounding.AwayFromZero);
        // Noms exacts seulement : Enum.TryParse accepterait aussi « 1 » (converti en Gram).
        if (!Enum.GetNames<QuantityUnit>().Contains(unit)
            || !Enum.TryParse<QuantityUnit>(unit, out var parsed)
            || rounded is < MinQuantity or > MaxQuantity)
        {
            return (1, QuantityUnit.Piece);
        }
        return (rounded, parsed);
    }
}
