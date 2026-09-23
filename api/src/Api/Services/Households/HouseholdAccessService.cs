using Api.Data;
using Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Households;

public interface IHouseholdAccessService
{
    /// <summary>
    /// Rôle de l'utilisateur dans le foyer, ou null s'il n'en est pas membre.
    /// Point central du contrôle d'accès : toute donnée d'un foyer passe par cette vérification.
    /// </summary>
    Task<HouseholdRole?> GetRoleAsync(Guid userId, Guid householdId, CancellationToken ct);
}

public sealed class HouseholdAccessService(AppDbContext db) : IHouseholdAccessService
{
    public Task<HouseholdRole?> GetRoleAsync(Guid userId, Guid householdId, CancellationToken ct) =>
        db.HouseholdMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.HouseholdId == householdId)
            .Select(m => (HouseholdRole?)m.Role)
            .SingleOrDefaultAsync(ct);
}
