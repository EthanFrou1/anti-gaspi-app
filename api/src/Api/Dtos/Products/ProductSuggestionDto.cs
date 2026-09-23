using Api.Entities;

namespace Api.Dtos.Products;

/// <summary>
/// Suggestion de pré-remplissage à partir d'un code-barres. Tous les champs sont
/// facultatifs : l'utilisateur complète ou corrige avant d'ajouter le produit.
/// </summary>
public sealed record ProductSuggestionDto(
    string Barcode,
    string? Name,
    string? Brand,
    // null si aucune étiquette Open Food Facts ne correspond à une catégorie de l'app.
    int? CategoryId,
    decimal? Quantity,
    QuantityUnit? Unit,
    string? ImageUrl,
    // Licence ODbL : la source doit être affichée avec les données.
    string Source);
