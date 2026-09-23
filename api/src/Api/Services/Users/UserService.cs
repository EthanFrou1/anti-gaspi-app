using Api.Common;
using Api.Data;
using Api.Dtos.Users;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Users;

public sealed class UserService(AppDbContext db) : IUserService
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
}
