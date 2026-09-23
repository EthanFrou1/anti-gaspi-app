namespace Api.Entities;

/// <summary>
/// Produit de l'inventaire. Il appartient au foyer (pas à l'utilisateur) ;
/// un propriétaire optionnel en fait un produit perso (cas des colocs).
/// </summary>
public class InventoryItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }
    public Household Household { get; set; } = null!;

    public required string Name { get; set; }

    public int CategoryId { get; set; }
    public ProductCategory Category { get; set; } = null!;

    public decimal Quantity { get; set; }

    public QuantityUnit Unit { get; set; }

    // DateOnly (type SQL « date ») : une péremption n'a ni heure ni fuseau horaire.
    public DateOnly PurchasedOn { get; set; }

    public DateOnly ExpiresOn { get; set; }

    // true si la date vient de la durée de la catégorie, false si l'utilisateur l'a saisie.
    public bool ExpiryIsEstimated { get; set; }

    public string? Barcode { get; set; }

    // null = produit commun au foyer ; renseigné = produit perso.
    public Guid? OwnerUserId { get; set; }
    public User? Owner { get; set; }

    public InventoryItemStatus Status { get; set; } = InventoryItemStatus.Active;

    // Date du passage à « consommé » ou « jeté » (base du futur compteur de gaspillage).
    public DateTimeOffset? StatusChangedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public enum QuantityUnit
{
    Piece = 0,
    Gram = 1,
    Kilogram = 2,
    Milliliter = 3,
    Liter = 4,
}

public enum InventoryItemStatus
{
    Active = 0,
    Consumed = 1,
    Discarded = 2,
}
