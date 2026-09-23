using Api.Common;
using Api.Dtos.Users;

namespace Api.Services.Users;

public static class UserErrors
{
    public static readonly Error NotFound = new(
        ErrorType.NotFound, "user.not_found", "Utilisateur introuvable.");

    public static readonly Error InvalidPassword = new(
        ErrorType.Validation,
        "user.invalid_password",
        "Mot de passe incorrect.",
        new Dictionary<string, string[]>
        {
            [nameof(DeleteAccountRequest.Password)] = ["Mot de passe incorrect."],
        });
}
