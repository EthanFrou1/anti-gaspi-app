using System.ComponentModel.DataAnnotations;
using Api.Entities;

namespace Api.Dtos.Inventory;

public sealed record InventoryItemDto(
    Guid Id,
    string Name,
    int CategoryId,
    decimal Quantity,
    QuantityUnit Unit,
    DateOnly PurchasedOn,
    DateOnly ExpiresOn,
    bool ExpiryIsEstimated,
    // Repris de la catégorie : l'app en a besoin pour ne pas afficher une DDM dépassée comme « périmée ».
    ExpiryKind ExpiryKind,
    string? Barcode,
    Guid? OwnerUserId,
    string? OwnerDisplayName,
    InventoryItemStatus Status,
    DateTimeOffset? StatusChangedAt,
    DateTimeOffset CreatedAt);

/// <summary>
/// Données d'un produit, à la création comme à la modification (remplacement complet).
/// </summary>
public sealed record SaveInventoryItemRequest(
    [Required(ErrorMessage = "Le nom du produit est obligatoire.")]
    [StringLength(100, ErrorMessage = "Le nom ne doit pas dépasser 100 caractères.")]
    string Name,

    [Range(1, int.MaxValue, ErrorMessage = "La catégorie est obligatoire.")]
    int CategoryId,

    // Culture invariante obligatoire : sinon, sur une machine en français, « 0.001 » ne se lit
    // pas (séparateur décimal = virgule) et la validation lève une exception (erreur 500).
    [Range(typeof(decimal), "0.001", "100000",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "La quantité doit être comprise entre 0,001 et 100 000.")]
    decimal Quantity,

    QuantityUnit Unit,

    // Date d'achat selon le calendrier de l'utilisateur (l'app l'envoie toujours ;
    // le serveur ne peut pas deviner « aujourd'hui » dans le fuseau de l'utilisateur).
    DateOnly PurchasedOn,

    // null = date estimée à partir de la catégorie. En modification, l'app renvoie null
    // tant que l'utilisateur n'a pas saisi de date : l'estimation suit alors la catégorie.
    DateOnly? ExpiresOn,

    [RegularExpression(@"^\d{8,14}$", ErrorMessage = "Le code-barres doit contenir 8 à 14 chiffres.")]
    string? Barcode,

    // true = produit perso de l'utilisateur qui l'enregistre ; false = produit commun.
    bool IsPersonal);

/// <summary>
/// « Consommé » ou « jeté », en entier ou en partie (« 200 g sur 600 g »). Corps facultatif :
/// sans quantité, c'est le produit entier.
/// </summary>
public sealed record ChangeStatusRequest(
    // Même unité que le produit. Culture invariante : voir SaveInventoryItemRequest.Quantity.
    [Range(typeof(decimal), "0.001", "100000",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "La quantité doit être comprise entre 0,001 et 100 000.")]
    decimal? Quantity);

/// <summary>
/// Ajout groupé : lignes validées par l'utilisateur après la lecture d'un ticket de caisse.
/// </summary>
public sealed record CreateInventoryItemsRequest(
    [Required(ErrorMessage = "La liste des produits est obligatoire.")]
    [Length(1, CreateInventoryItemsRequest.MaxItems, ErrorMessage = "Ajoute entre 1 et 60 produits à la fois.")]
    IReadOnlyList<SaveInventoryItemRequest> Items)
{
    // Même limite que la lecture d'un ticket (60 lignes au plus).
    public const int MaxItems = 60;
}
