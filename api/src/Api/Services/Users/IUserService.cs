using Api.Common;
using Api.Dtos.Users;

namespace Api.Services.Users;

public interface IUserService
{
    Task<Result<UserDto>> GetAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Supprime définitivement le compte (RGPD), après vérification du mot de passe.
    /// Applique la règle de propriété du foyer avant la suppression.
    /// </summary>
    Task<Result> DeleteAccountAsync(Guid userId, string password, CancellationToken ct);
}
