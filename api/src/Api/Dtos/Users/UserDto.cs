namespace Api.Dtos.Users;

// HouseholdId est null tant que l'utilisateur n'a pas créé ou rejoint de foyer.
// HasProfile est false tant que l'onboarding (préférences alimentaires) n'est pas terminé.
public sealed record UserDto(Guid Id, string Email, string DisplayName, Guid? HouseholdId, bool HasProfile);
