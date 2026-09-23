using Api.Common;
using Api.Data;
using Api.Dtos.Households;
using Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Households;

public interface IHouseholdService
{
    Task<Result<HouseholdDto>> CreateAsync(Guid userId, CreateHouseholdRequest request, CancellationToken ct);

    Task<Result<HouseholdDto>> GetMineAsync(Guid userId, CancellationToken ct);

    Task<Result<HouseholdDto>> JoinAsync(Guid userId, string code, CancellationToken ct);

    /// <summary>
    /// Retire un membre du foyer. Un membre peut se retirer lui-même (quitter) ;
    /// seul le propriétaire peut retirer quelqu'un d'autre.
    /// </summary>
    Task<Result> RemoveMemberAsync(Guid actorId, Guid householdId, Guid targetUserId, CancellationToken ct);

    /// <summary>
    /// Fait quitter à l'utilisateur son foyer actuel, s'il en a un (utilisé à la suppression du compte).
    /// </summary>
    Task<Result> LeaveCurrentAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Remplace l'équipement de la cuisine du foyer (tout membre peut le modifier).
    /// </summary>
    Task<Result<HouseholdDto>> UpdateEquipmentAsync(
        Guid actorId, Guid householdId, IReadOnlyList<KitchenEquipment> equipment, CancellationToken ct);
}

public sealed class HouseholdService(AppDbContext db, TimeProvider time) : IHouseholdService
{
    public async Task<Result<HouseholdDto>> CreateAsync(Guid userId, CreateHouseholdRequest request, CancellationToken ct)
    {
        if (await db.HouseholdMembers.AnyAsync(m => m.UserId == userId, ct))
        {
            return HouseholdErrors.AlreadyMember;
        }

        var now = time.GetUtcNow();
        var household = new Household { Name = request.Name.Trim(), CreatedAt = now };
        household.Members.Add(new HouseholdMember { UserId = userId, Role = HouseholdRole.Owner, JoinedAt = now });
        db.Households.Add(household);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Deux créations simultanées : l'index unique sur UserId a bloqué la seconde.
            return HouseholdErrors.AlreadyMember;
        }

        return await GetMineAsync(userId, ct);
    }

    public async Task<Result<HouseholdDto>> GetMineAsync(Guid userId, CancellationToken ct)
    {
        var membership = await db.HouseholdMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new { m.HouseholdId, m.Role, m.Household.Name, m.Household.CreatedAt, m.Household.Equipment })
            .SingleOrDefaultAsync(ct);

        if (membership is null)
        {
            return HouseholdErrors.NoHousehold;
        }

        var members = await db.HouseholdMembers
            .AsNoTracking()
            .Where(m => m.HouseholdId == membership.HouseholdId)
            .OrderBy(m => m.JoinedAt)
            .Select(m => new HouseholdMemberDto(m.UserId, m.User.DisplayName, m.Role, m.JoinedAt))
            .ToListAsync(ct);

        return new HouseholdDto(
            membership.HouseholdId, membership.Name, membership.CreatedAt, membership.Role, membership.Equipment, members);
    }

    public Task<Result<HouseholdDto>> JoinAsync(Guid userId, string code, CancellationToken ct) =>
        db.InTransactionAsync(async () =>
        {
            if (await db.HouseholdMembers.AnyAsync(m => m.UserId == userId, ct))
            {
                return (Result<HouseholdDto>)HouseholdErrors.AlreadyMember;
            }

            var now = time.GetUtcNow();
            var normalizedCode = InvitationCode.Normalize(code);
            var householdId = await db.HouseholdInvitations
                .Where(i => i.Code == normalizedCode && i.RevokedAt == null && i.ExpiresAt > now)
                .Select(i => (Guid?)i.HouseholdId)
                .SingleOrDefaultAsync(ct);

            // Le verrou empêche de rejoindre un foyer en train d'être supprimé
            // (son dernier membre part au même moment).
            if (householdId is null || !await db.LockHouseholdAsync(householdId.Value, ct))
            {
                return HouseholdErrors.InvitationInvalid;
            }

            db.HouseholdMembers.Add(new HouseholdMember
            {
                HouseholdId = householdId.Value,
                UserId = userId,
                Role = HouseholdRole.Member,
                JoinedAt = now,
            });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                return HouseholdErrors.AlreadyMember;
            }

            return await GetMineAsync(userId, ct);
        }, ct);

    public Task<Result> RemoveMemberAsync(Guid actorId, Guid householdId, Guid targetUserId, CancellationToken ct) =>
        db.InTransactionAsync(async () =>
        {
            if (!await db.LockHouseholdAsync(householdId, ct))
            {
                return (Result)HouseholdErrors.NotFound;
            }

            var members = await db.HouseholdMembers.Where(m => m.HouseholdId == householdId).ToListAsync(ct);

            var actor = members.SingleOrDefault(m => m.UserId == actorId);
            if (actor is null)
            {
                return HouseholdErrors.NotFound;
            }

            if (actorId != targetUserId && actor.Role != HouseholdRole.Owner)
            {
                return HouseholdErrors.OwnerOnly;
            }

            var target = members.SingleOrDefault(m => m.UserId == targetUserId);
            if (target is null)
            {
                return HouseholdErrors.MemberNotFound;
            }

            if (actorId != targetUserId)
            {
                // Exclusion : on révoque toutes les invitations actives, sinon le membre
                // exclu pourrait revenir avec un code qu'il connaît déjà.
                var now = time.GetUtcNow();
                await db.HouseholdInvitations
                    .Where(i => i.HouseholdId == householdId && i.RevokedAt == null && i.ExpiresAt > now)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.RevokedAt, now), ct);
            }

            await RemoveMembershipAsync(target, members, ct);
            return Result.Success();
        }, ct);

    public async Task<Result<HouseholdDto>> UpdateEquipmentAsync(
        Guid actorId, Guid householdId, IReadOnlyList<KitchenEquipment> equipment, CancellationToken ct)
    {
        if (!equipment.All(Enum.IsDefined))
        {
            return new Error(ErrorType.Validation, "household.validation", "Équipement inconnu.",
                new Dictionary<string, string[]> { ["Equipment"] = ["Équipement inconnu."] });
        }

        var household = await db.Households.SingleOrDefaultAsync(h => h.Id == householdId, ct);
        if (household is null)
        {
            return HouseholdErrors.NotFound;
        }

        household.Equipment = equipment.Distinct().Order().ToList();
        await db.SaveChangesAsync(ct);
        return await GetMineAsync(actorId, ct);
    }

    public Task<Result> LeaveCurrentAsync(Guid userId, CancellationToken ct) =>
        db.InTransactionAsync(async () =>
        {
            var householdId = await db.HouseholdMembers
                .Where(m => m.UserId == userId)
                .Select(m => (Guid?)m.HouseholdId)
                .SingleOrDefaultAsync(ct);

            if (householdId is null || !await db.LockHouseholdAsync(householdId.Value, ct))
            {
                return Result.Success();
            }

            // Relu après le verrou : la composition du foyer a pu changer entre-temps.
            var members = await db.HouseholdMembers.Where(m => m.HouseholdId == householdId).ToListAsync(ct);
            var target = members.SingleOrDefault(m => m.UserId == userId);
            if (target is not null)
            {
                await RemoveMembershipAsync(target, members, ct);
            }

            return Result.Success();
        }, ct);

    /// <summary>
    /// Règle de propriété du foyer (voir CLAUDE.md) :
    /// - ses produits perso deviennent communs ;
    /// - s'il ne reste personne, le foyer et tout son contenu sont supprimés ;
    /// - si le propriétaire part, la propriété passe au membre le plus ancien.
    /// Doit être appelée dans une transaction, foyer verrouillé.
    /// </summary>
    private async Task RemoveMembershipAsync(HouseholdMember leaving, List<HouseholdMember> members, CancellationToken ct)
    {
        var remaining = members
            .Where(m => m.UserId != leaving.UserId)
            .OrderBy(m => m.JoinedAt)
            .ThenBy(m => m.UserId) // départage stable en cas d'égalité parfaite
            .ToList();

        // Ses produits perso deviennent communs : la nourriture reste dans le frigo.
        await db.InventoryItems
            .Where(i => i.HouseholdId == leaving.HouseholdId && i.OwnerUserId == leaving.UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.OwnerUserId, (Guid?)null), ct);

        db.HouseholdMembers.Remove(leaving);

        if (remaining.Count == 0)
        {
            // Les invitations (et plus tard l'inventaire) sont supprimées par la base
            // grâce aux suppressions en cascade.
            var household = await db.Households.SingleAsync(h => h.Id == leaving.HouseholdId, ct);
            db.Households.Remove(household);
        }
        else if (leaving.Role == HouseholdRole.Owner)
        {
            remaining[0].Role = HouseholdRole.Owner;
        }

        await db.SaveChangesAsync(ct);
    }
}
