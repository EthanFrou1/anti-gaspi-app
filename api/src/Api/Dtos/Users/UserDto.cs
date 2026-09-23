namespace Api.Dtos.Users;

// HouseholdId est null tant que l'utilisateur n'a pas créé ou rejoint de foyer.
public sealed record UserDto(Guid Id, string Email, string DisplayName, Guid? HouseholdId);
