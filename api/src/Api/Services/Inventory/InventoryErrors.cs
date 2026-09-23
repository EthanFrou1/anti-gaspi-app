using Api.Common;
using Api.Dtos.Inventory;

namespace Api.Services.Inventory;

public static class InventoryErrors
{
    public static readonly Error ItemNotFound = new(
        ErrorType.NotFound, "inventory.item_not_found", "Produit introuvable.");

    public static readonly Error NotOwner = new(
        ErrorType.Forbidden, "inventory.not_owner", "Ce produit perso appartient à un autre membre du foyer.");

    public static readonly Error NotActive = new(
        ErrorType.Conflict, "inventory.not_active", "Ce produit a déjà été marqué comme consommé ou jeté.");

    public static readonly Error LimitReached = new(
        ErrorType.Conflict,
        "inventory.limit_reached",
        $"Le foyer a atteint la limite de {InventoryService.MaxActiveItemsPerHousehold} produits en stock.");

    public static readonly Error UnknownCategory = Validation(
        nameof(SaveInventoryItemRequest.CategoryId), "Cette catégorie n'existe pas.");

    public static readonly Error PurchaseDateOutOfRange = Validation(
        nameof(SaveInventoryItemRequest.PurchasedOn), "La date d'achat doit être comprise entre il y a 5 ans et demain.");

    public static readonly Error ExpiryDateOutOfRange = Validation(
        nameof(SaveInventoryItemRequest.ExpiresOn), "La date de péremption doit être comprise entre un an avant l'achat et 10 ans après.");

    private static Error Validation(string field, string message) =>
        new(ErrorType.Validation, "inventory.validation", message, new Dictionary<string, string[]> { [field] = [message] });
}
