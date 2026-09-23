using Api.Entities;

namespace Api.Dtos.Inventory;

// DefaultShelfLifeDays est exposé pour que l'app affiche un aperçu de la date
// estimée pendant la saisie ; l'estimation qui fait foi reste celle de l'API.
public sealed record CategoryDto(int Id, string Code, string Name, int DefaultShelfLifeDays, ExpiryKind ExpiryKind);
