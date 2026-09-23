using Api.Common;
using Api.Data;
using Api.Dtos.Users;
using Api.Entities;
using Api.Services.Auth;
using Api.Services.Households;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Users;

public sealed class UserService(
    AppDbContext db,
    UserManager<User> userManager,
    IHouseholdService householdService) : IUserService
{
    public async Task<Result<UserDto>> GetAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserDto(
                u.Id,
                u.Email!,
                u.DisplayName,
                u.Membership == null ? null : u.Membership.HouseholdId))
            .SingleOrDefaultAsync(ct);

        // Cas possible : compte supprimé alors que son access token est encore valide.
        return user is null ? UserErrors.NotFound : user;
    }

    public async Task<Result> DeleteAccountAsync(Guid userId, string password, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        // Même protection contre la force brute qu'à la connexion.
        if (await userManager.IsLockedOutAsync(user))
        {
            return AuthErrors.LockedOut;
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            return UserErrors.InvalidPassword;
        }

        // Une seule transaction : soit le compte est supprimé ET le foyer mis à jour,
        // soit rien ne change.
        return await db.InTransactionAsync(async () =>
        {
            var leave = await householdService.LeaveCurrentAsync(userId, ct);
            if (!leave.IsSuccess)
            {
                return leave;
            }

            // Les refresh tokens sont supprimés en cascade ; les invitations créées
            // par l'utilisateur restent valables (auteur mis à null).
            var deleted = await userManager.DeleteAsync(user);
            if (!deleted.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Échec de la suppression du compte {userId} : {string.Join(", ", deleted.Errors.Select(e => e.Code))}");
            }

            return Result.Success();
        }, ct);
    }
}
