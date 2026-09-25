using System.Text.Json;

namespace Api.Services.Receipts;

/// <summary>
/// Photo d'un ticket de caisse, déjà compressée par l'app. Elle reste en mémoire le temps
/// de la requête et n'est JAMAIS enregistrée (RGPD).
/// </summary>
public sealed record ReceiptImage(byte[] Data, string MediaType);

/// <summary>
/// Prompt d'une lecture de ticket : ce qui accompagne l'image vers l'IA.
/// </summary>
public sealed record ReceiptPrompt(
    string System,
    string UserContent,
    IReadOnlyDictionary<string, JsonElement> OutputSchema);

/// <summary>
/// Réponse brute de l'IA (format imposé par le schéma JSON), avant validation.
/// Catégorie et unité restent du texte : le validateur les vérifie et les convertit.
/// </summary>
public sealed record ReceiptDraft(string? PurchaseDate, IReadOnlyList<ReceiptDraftLine>? Lines);

/// <summary>
/// Ligne lue par le modèle. Il recopie les nombres imprimés ; l'API fait tous les calculs
/// (ReceiptValidator). Nombres décimaux et facultatifs, pour qu'une valeur inattendue
/// (« 2.5 », absente) ne fasse pas échouer toute la lecture : le validateur la corrige.
/// </summary>
/// <param name="Quantity">Contenu d'UNE unité du lot (« 125 » pour « 4X125G »), dans l'unité Unit.</param>
/// <param name="Unit">Une unité de l'app, ou « Centiliter » (lecture seulement, convertie en millilitres).</param>
/// <param name="Copies">Nombre d'exemplaires lu (multiplicateur « 6 x 0,99 »).</param>
/// <param name="LinePrice">Prix de la ligne. Sert seulement à vérifier Copies : jamais renvoyé ni enregistré.</param>
/// <param name="UnitPrice">Prix d'un exemplaire imprimé avec le multiplicateur. Même usage que LinePrice.</param>
/// <param name="PackSize">Lot écrit dans le libellé (6 pour « 6X1,5L »).</param>
public sealed record ReceiptDraftLine(
    string? ReceiptText,
    string? Name,
    string? Category,
    decimal Quantity,
    string? Unit,
    bool IsFood,
    decimal? Copies = null,
    decimal? LinePrice = null,
    decimal? UnitPrice = null,
    decimal? PackSize = null);

/// <summary>
/// Point d'entrée unique vers l'IA pour les tickets : changer de fournisseur = écrire une
/// autre implémentation. Lève AiUnavailableException si la lecture est impossible.
/// </summary>
public interface IReceiptReader
{
    Task<ReceiptDraft> ReadAsync(ReceiptImage image, ReceiptPrompt prompt, CancellationToken ct);
}
