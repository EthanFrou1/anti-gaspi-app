using Api.Common;
using Api.Data;
using Api.Dtos.Households;
using Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Households;

/// <summary>
/// Gestion des invitations. L'appartenance au foyer est vérifiée en amont par la
/// politique d'autorisation HouseholdMember ; les règles plus fines sont ici.
/// </summary>
public interface IInvitationService
{
    Task<Result<InvitationDto>> CreateAsync(Guid actorId, Guid householdId, CancellationToken ct);

    Task<IReadOnlyList<InvitationDto>> ListActiveAsync(Guid householdId, CancellationToken ct);

    /// <summary>
    /// Un membre peut révoquer ses propres invitations ; le propriétaire peut toutes les révoquer.
    /// </summary>
    Task<Result> RevokeAsync(Guid actorId, Guid householdId, Guid invitationId, CancellationToken ct);
}

public sealed class InvitationService(
    AppDbContext db,
    IHouseholdAccessService access,
    TimeProvider time) : IInvitationService
{
    public static readonly TimeSpan Validity = TimeSpan.FromDays(7);

    // Évite qu'un foyer accumule des codes valides (chacun est une porte d'entrée).
    public const int MaxActivePerHousehold = 10;

    private const int MaxGenerationAttempts = 5;

    public Task<Result<InvitationDto>> CreateAsync(Guid actorId, Guid householdId, CancellationToken ct) =>
        db.InTransactionAsync(async () =>
        {
            // Verrou : deux créations simultanées ne peuvent pas dépasser le plafond ensemble.
            if (!await db.LockHouseholdAsync(householdId, ct))
            {
                return (Result<InvitationDto>)HouseholdErrors.NotFound;
            }

            var now = time.GetUtcNow();
            var activeCount = await db.HouseholdInvitations
                .CountAsync(i => i.HouseholdId == householdId && i.RevokedAt == null && i.ExpiresAt > now, ct);

            if (activeCount >= MaxActivePerHousehold)
            {
                return HouseholdErrors.InvitationLimitReached;
            }

            return ToDto(await AddWithUniqueCodeAsync(actorId, householdId, now, ct));
        }, ct);

    public async Task<IReadOnlyList<InvitationDto>> ListActiveAsync(Guid householdId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return await db.HouseholdInvitations
            .AsNoTracking()
            .Where(i => i.HouseholdId == householdId && i.RevokedAt == null && i.ExpiresAt > now)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvitationDto(i.Id, i.Code, i.ExpiresAt, i.CreatedByUserId))
            .ToListAsync(ct);
    }

    public async Task<Result> RevokeAsync(Guid actorId, Guid householdId, Guid invitationId, CancellationToken ct)
    {
        // Le filtre sur HouseholdId est essentiel : sans lui, un membre d'un foyer
        // pourrait révoquer l'invitation d'un autre foyer en devinant son identifiant.
        var invitation = await db.HouseholdInvitations
            .SingleOrDefaultAsync(i => i.Id == invitationId && i.HouseholdId == householdId && i.RevokedAt == null, ct);

        if (invitation is null)
        {
            return HouseholdErrors.InvitationNotFound;
        }

        if (invitation.CreatedByUserId != actorId
            && await access.GetRoleAsync(actorId, householdId, ct) != HouseholdRole.Owner)
        {
            return HouseholdErrors.InvitationNotCreator;
        }

        invitation.RevokedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<HouseholdInvitation> AddWithUniqueCodeAsync(
        Guid actorId, Guid householdId, DateTimeOffset now, CancellationToken ct)
    {
        // Une collision de code est très improbable, mais on la gère sans exception
        // (une erreur SQL annulerait la transaction en cours).
        for (var attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            var code = InvitationCode.Generate();
            if (await db.HouseholdInvitations.AnyAsync(i => i.Code == code, ct))
            {
                continue;
            }

            var invitation = new HouseholdInvitation
            {
                HouseholdId = householdId,
                Code = code,
                CreatedByUserId = actorId,
                CreatedAt = now,
                ExpiresAt = now.Add(Validity),
            };
            db.HouseholdInvitations.Add(invitation);
            await db.SaveChangesAsync(ct);
            return invitation;
        }

        throw new InvalidOperationException("Impossible de générer un code d'invitation unique.");
    }

    private static InvitationDto ToDto(HouseholdInvitation invitation) =>
        new(invitation.Id, invitation.Code, invitation.ExpiresAt, invitation.CreatedByUserId);
}
