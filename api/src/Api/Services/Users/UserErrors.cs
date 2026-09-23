using Api.Common;

namespace Api.Services.Users;

public static class UserErrors
{
    public static readonly Error NotFound = new(
        ErrorType.NotFound, "user.not_found", "Utilisateur introuvable.");
}
