using Microsoft.AspNetCore.Identity;

namespace Api.Services.Auth;

/// <summary>
/// Traduit les messages d'erreur d'Identity affichés à l'utilisateur.
/// Les codes (Code) restent inchangés : c'est sur eux que s'appuie le mapping des erreurs.
/// </summary>
public sealed class FrenchIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) => new()
    {
        Code = nameof(PasswordTooShort),
        Description = $"Le mot de passe doit contenir au moins {length} caractères.",
    };

    public override IdentityError InvalidEmail(string? email) => new()
    {
        Code = nameof(InvalidEmail),
        Description = "L'email n'est pas valide.",
    };

    public override IdentityError DuplicateEmail(string email) => new()
    {
        Code = nameof(DuplicateEmail),
        Description = "Un compte existe déjà avec cet email.",
    };

    public override IdentityError DuplicateUserName(string userName) => new()
    {
        Code = nameof(DuplicateUserName),
        Description = "Un compte existe déjà avec cet email.",
    };
}
