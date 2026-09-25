using Api.Common;
using Api.Dtos.Households;
using Api.Entities;
using Api.Services.Households;
using Api.Services.Users;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Households;

/// <summary>
/// Helpers communs aux tests des foyers. Chaque appel passe par un scope neuf,
/// comme une requête HTTP distincte.
/// </summary>
public abstract class HouseholdTestBase(DatabaseFixture database) : DatabaseTestBase(database)
{
    protected async Task<Guid> CreateHouseholdAsync(Guid ownerId, string name = "Coloc")
    {
        var result = await CreateHouseholdResultAsync(ownerId, name);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value.Id;
    }

    protected Task<Result<HouseholdDto>> CreateHouseholdResultAsync(Guid ownerId, string name = "Coloc") =>
        WithServiceAsync<IHouseholdService, Result<HouseholdDto>>(s =>
            s.CreateAsync(ownerId, new CreateHouseholdRequest(name), CancellationToken.None));

    protected async Task<string> CreateInvitationCodeAsync(Guid ownerId, Guid householdId)
    {
        var result = await WithServiceAsync<IInvitationService, Result<InvitationDto>>(s =>
            s.CreateAsync(ownerId, householdId, CancellationToken.None));
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value.Code;
    }

    /// <summary>
    /// Code actif du foyer, créé s'il n'y en a pas (un seul code actif par foyer, comme dans l'app).
    /// </summary>
    protected async Task<string> ActiveInvitationCodeAsync(Guid actorId, Guid householdId)
    {
        var active = await WithServiceAsync<IInvitationService, IReadOnlyList<InvitationDto>>(s =>
            s.ListActiveAsync(householdId, CancellationToken.None));
        return active.Count > 0 ? active[0].Code : await CreateInvitationCodeAsync(actorId, householdId);
    }

    protected Task<Result<HouseholdDto>> JoinAsync(Guid userId, string code) =>
        WithServiceAsync<IHouseholdService, Result<HouseholdDto>>(s =>
            s.JoinAsync(userId, code, CancellationToken.None));

    /// <summary>
    /// Fait rejoindre le foyer une heure plus tard que l'arrivée précédente :
    /// l'ordre d'ancienneté est ainsi explicite dans les tests.
    /// </summary>
    protected async Task AddMemberAsync(Guid ownerId, Guid householdId, Guid userId)
    {
        var code = await ActiveInvitationCodeAsync(ownerId, householdId);
        Clock.Advance(TimeSpan.FromHours(1));
        var result = await JoinAsync(userId, code);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    protected Task<Result> RemoveMemberAsync(Guid actorId, Guid householdId, Guid targetUserId) =>
        WithServiceAsync<IHouseholdService, Result>(s =>
            s.RemoveMemberAsync(actorId, householdId, targetUserId, CancellationToken.None));

    protected Task<Result> DeleteAccountAsync(Guid userId, string password = DefaultPassword) =>
        WithServiceAsync<IUserService, Result>(s =>
            s.DeleteAccountAsync(userId, password, CancellationToken.None));

    protected async Task<Dictionary<Guid, HouseholdRole>> GetMembersAsync(Guid householdId)
    {
        await using var db = CreateDbContext();
        return await db.HouseholdMembers
            .Where(m => m.HouseholdId == householdId)
            .ToDictionaryAsync(m => m.UserId, m => m.Role);
    }

    protected async Task<bool> HouseholdExistsAsync(Guid householdId)
    {
        await using var db = CreateDbContext();
        return await db.Households.AnyAsync(h => h.Id == householdId);
    }
}
