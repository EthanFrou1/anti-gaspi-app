using Api.Common;
using Api.Dtos.Households;
using Api.Services.Households;
using Api.Tests.Infrastructure;

namespace Api.Tests.Households;

public class InvitationServiceTests(DatabaseFixture database) : HouseholdTestBase(database)
{
    // ---------- Création ----------

    [Fact]
    public async Task Create_ReturnsCodeValidForSevenDays()
    {
        var alice = await CreateUserAsync("Alice");
        var householdId = await CreateHouseholdAsync(alice);

        var result = await CreateAsync(alice, householdId);

        Assert.True(result.IsSuccess);
        Assert.Equal(InvitationCode.Length, result.Value.Code.Length);
        Assert.Equal(Clock.Now.AddDays(7), result.Value.ExpiresAt);
        Assert.Equal(alice, result.Value.CreatedByUserId);
    }

    [Fact]
    public async Task Create_ByASimpleMember_IsAllowed()
    {
        var (_, bob, householdId) = await CreateHouseholdWithMemberAsync();

        var result = await CreateAsync(bob, householdId);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Create_WhenACodeIsAlreadyActive_IsRejected_ForEveryMember()
    {
        var (owner, bob, householdId) = await CreateHouseholdWithMemberAsync();
        Assert.True((await CreateAsync(owner, householdId)).IsSuccess);

        // Un code est actif : ni Bob ni le propriétaire n'en créent un second, ils partagent celui-ci.
        Assert.Equal(HouseholdErrors.InvitationLimitReached, (await CreateAsync(bob, householdId)).Error);
        Assert.Equal(HouseholdErrors.InvitationLimitReached, (await CreateAsync(owner, householdId)).Error);
        Assert.Single(await ListAsync(householdId));
    }

    [Fact]
    public async Task Create_AfterTheActiveCodeIsRevokedOrExpired_IsAllowed()
    {
        var alice = await CreateUserAsync("Alice");
        var householdId = await CreateHouseholdAsync(alice);
        var first = (await CreateAsync(alice, householdId)).Value!;

        // Un code révoqué libère la place…
        await RevokeAsync(alice, householdId, first.Id);
        var second = await CreateAsync(alice, householdId);
        Assert.True(second.IsSuccess);

        // …tout comme un code expiré (7 jours).
        Clock.Advance(TimeSpan.FromDays(7) + TimeSpan.FromMinutes(1));
        Assert.Empty(await ListAsync(householdId));
        Assert.True((await CreateAsync(alice, householdId)).IsSuccess);
    }

    [Fact]
    public async Task Create_ConcurrentRequests_CreateASingleCode()
    {
        var (owner, bob, householdId) = await CreateHouseholdWithMemberAsync();

        // Deux membres demandent un code au même moment : le verrou du foyer n'en laisse passer qu'un.
        var results = await Task.WhenAll(CreateAsync(owner, householdId), CreateAsync(bob, householdId));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(await ListAsync(householdId));
    }

    // ---------- Consultation ----------

    [Fact]
    public async Task ListActive_ExcludesRevokedAndExpiredInvitations()
    {
        var alice = await CreateUserAsync("Alice");
        var householdId = await CreateHouseholdAsync(alice);
        var revoked = (await CreateAsync(alice, householdId)).Value!;
        await RevokeAsync(alice, householdId, revoked.Id);
        await CreateAsync(alice, householdId);
        Clock.Advance(TimeSpan.FromDays(8)); // ce deuxième code a maintenant expiré
        var active = (await CreateAsync(alice, householdId)).Value!;

        var list = await ListAsync(householdId);

        var only = Assert.Single(list);
        Assert.Equal(active.Id, only.Id);
    }

    // ---------- Révocation ----------

    [Fact]
    public async Task Revoke_MemberCanRevokeTheirOwnInvitation()
    {
        var (_, bob, householdId) = await CreateHouseholdWithMemberAsync();
        var invitation = (await CreateAsync(bob, householdId)).Value!;

        var result = await RevokeAsync(bob, householdId, invitation.Id);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Revoke_MemberCannotRevokeSomeoneElsesInvitation()
    {
        var (owner, bob, householdId) = await CreateHouseholdWithMemberAsync();
        var invitation = (await CreateAsync(owner, householdId)).Value!;

        var result = await RevokeAsync(bob, householdId, invitation.Id);

        Assert.Equal(HouseholdErrors.InvitationNotCreator, result.Error);
        Assert.Contains(await ListAsync(householdId), i => i.Id == invitation.Id);
    }

    [Fact]
    public async Task Revoke_OwnerCanRevokeAMembersInvitation()
    {
        var (owner, bob, householdId) = await CreateHouseholdWithMemberAsync();
        var invitation = (await CreateAsync(bob, householdId)).Value!;

        var result = await RevokeAsync(owner, householdId, invitation.Id);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(await ListAsync(householdId), i => i.Id == invitation.Id);
    }

    [Fact]
    public async Task Revoke_InvitationOfAnotherHousehold_ReturnsNotFoundAndKeepsIt()
    {
        // Mallory est propriétaire de son propre foyer et tente de révoquer
        // l'invitation d'Alice en passant son propre foyer dans l'URL.
        var alice = await CreateUserAsync("Alice");
        var mallory = await CreateUserAsync("Mallory");
        var aliceHousehold = await CreateHouseholdAsync(alice);
        var malloryHousehold = await CreateHouseholdAsync(mallory, "Chez Mallory");
        var invitation = (await CreateAsync(alice, aliceHousehold)).Value!;

        var result = await RevokeAsync(mallory, malloryHousehold, invitation.Id);

        Assert.Equal(HouseholdErrors.InvitationNotFound, result.Error);
        var bob = await CreateUserAsync("Bob");
        Assert.True((await JoinAsync(bob, invitation.Code)).IsSuccess);
    }

    [Fact]
    public async Task Revoke_Twice_ReturnsNotFoundTheSecondTime()
    {
        var alice = await CreateUserAsync("Alice");
        var householdId = await CreateHouseholdAsync(alice);
        var invitation = (await CreateAsync(alice, householdId)).Value!;

        Assert.True((await RevokeAsync(alice, householdId, invitation.Id)).IsSuccess);
        Assert.Equal(HouseholdErrors.InvitationNotFound, (await RevokeAsync(alice, householdId, invitation.Id)).Error);
    }

    // ---------- Helpers ----------

    private async Task<(Guid Owner, Guid Member, Guid HouseholdId)> CreateHouseholdWithMemberAsync()
    {
        var owner = await CreateUserAsync("Owner");
        var bob = await CreateUserAsync("Bob");
        var householdId = await CreateHouseholdAsync(owner);
        await AddMemberAsync(owner, householdId, bob);
        // Le code qui a fait entrer Bob est révoqué : chaque test part d'un foyer sans code actif.
        foreach (var invitation in await ListAsync(householdId))
        {
            await RevokeAsync(owner, householdId, invitation.Id);
        }
        return (owner, bob, householdId);
    }

    private Task<Result<InvitationDto>> CreateAsync(Guid actorId, Guid householdId) =>
        WithServiceAsync<IInvitationService, Result<InvitationDto>>(s =>
            s.CreateAsync(actorId, householdId, CancellationToken.None));

    private Task<IReadOnlyList<InvitationDto>> ListAsync(Guid householdId) =>
        WithServiceAsync<IInvitationService, IReadOnlyList<InvitationDto>>(s =>
            s.ListActiveAsync(householdId, CancellationToken.None));

    private Task<Result> RevokeAsync(Guid actorId, Guid householdId, Guid invitationId) =>
        WithServiceAsync<IInvitationService, Result>(s =>
            s.RevokeAsync(actorId, householdId, invitationId, CancellationToken.None));
}
