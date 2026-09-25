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
    // Au-delà, sans doute un nombre mal lu (prix, code) plutôt qu'un achat.
    public const int MaxCopies = 99;

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
            var quantity = Quantities(line.Copies, line.Quantity, line.Unit);
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
    /// Quantité totale = nombre d'exemplaires × contenu d'un exemplaire. Le modèle lit les deux
    /// nombres, le code fait la multiplication : le modèle lit bien « 6 x » et « 1L », mais ne
    /// les multiplie pas de façon fiable (évaluation de design/test-tickets). Le total garde
    /// l'unité du contenu (3 × 400 g = 1 200 g). Total hors bornes : un seul exemplaire.
    /// </summary>
    public static (int Copies, decimal PerCopy, decimal Total, QuantityUnit Unit) Quantities(
        decimal? copies, decimal quantity, string? unit)
    {
        var (perCopy, parsedUnit) = Quantity(quantity, unit);
        var count = Copies(copies);
        var total = Math.Round(count * perCopy, 3, MidpointRounding.AwayFromZero);
        return total > MaxQuantity ? (1, perCopy, perCopy, parsedUnit) : (count, perCopy, total, parsedUnit);
    }

    /// <summary>
    /// Nombre d'exemplaires : un entier de 1 à MaxCopies. Absent, décimal ou hors bornes :
    /// 1 (l'article compte une fois, l'utilisateur corrige sur l'écran de validation).
    /// </summary>
    public static int Copies(decimal? copies) =>
        copies is { } value && value == decimal.Truncate(value) && value >= 1 && value <= MaxCopies ? (int)value : 1;

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
