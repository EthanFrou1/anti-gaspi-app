using Api.Common;

namespace Api.Services.Auth;

public static class AuthErrors
{
    // Même erreur pour « email inconnu » et « mauvais mot de passe » :
    // on ne révèle pas quels emails ont un compte.
    public static readonly Error InvalidCredentials = new(
        ErrorType.Unauthorized, "auth.invalid_credentials", "Email ou mot de passe incorrect.");

    public static readonly Error LockedOut = new(
        ErrorType.Unauthorized, "auth.locked_out", "Trop de tentatives échouées. Réessaie dans quelques minutes.");

    public static readonly Error InvalidRefreshToken = new(
        ErrorType.Unauthorized, "auth.invalid_refresh_token", "Ta session a expiré, reconnecte-toi.");

    public static readonly Error EmailAlreadyUsed = new(
        ErrorType.Conflict, "auth.email_taken", "Un compte existe déjà avec cet email.");
}
