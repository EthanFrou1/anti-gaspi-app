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

/// <param name="Quantity">Contenu d'UN exemplaire (« 1 » pour « LAIT 1L »), dans l'unité Unit.</param>
/// <param name="Copies">
/// Nombre d'exemplaires achetés : le modèle le lit, l'API calcule le total (ReceiptValidator).
/// Décimal et facultatif, pour qu'une valeur inattendue (« 2.5 », absente) ne fasse pas
/// échouer toute la lecture : le validateur la ramène à 1.
/// </param>
public sealed record ReceiptDraftLine(
    string? ReceiptText,
    string? Name,
    string? Category,
    decimal Quantity,
    string? Unit,
    bool IsFood,
    decimal? Copies = null);

/// <summary>
/// Point d'entrée unique vers l'IA pour les tickets : changer de fournisseur = écrire une
/// autre implémentation. Lève AiUnavailableException si la lecture est impossible.
/// </summary>
public interface IReceiptReader
{
    Task<ReceiptDraft> ReadAsync(ReceiptImage image, ReceiptPrompt prompt, CancellationToken ct);
}
