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
    }

    [Fact]
    public async Task Create_ByASimpleMember_IsAllowed()
    {
        var (_, bob, householdId) = await CreateHouseholdWithMemberAsync();

        var result = await CreateAsync(bob, householdId);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Create_BeyondTenActiveInvitations_IsRejected()
    {
        var alice = await CreateUserAsync("Alice");
        var householdId = await CreateHouseholdAsync(alice);
        for (var i = 0; i < InvitationService.MaxActivePerHousehold; i++)
        {
            Assert.True((await CreateAsync(alice, householdId)).IsSuccess);
        }

        var result = await CreateAsync(alice, householdId);

        Assert.Equal(HouseholdErrors.InvitationLimitReached, result.Error);
    }

    [Fact]
    public async Task Create_RevokedAndExpiredInvitationsDoNotCountTowardsTheLimit()
    {
        var alice = await CreateUserAsync("Alice");
        var householdId = await CreateHouseholdAsync(alice);
        var first = (await CreateAsync(alice, householdId)).Value!;
        Clock.Advance(TimeSpan.FromDays(6));
        for (var i = 1; i < InvitationService.MaxActivePerHousehold; i++)
        {
            await CreateAsync(alice, householdId);
        }

        Assert.Equal(HouseholdErrors.InvitationLimitReached, (await CreateAsync(alice, householdId)).Error);

        // Une invitation révoquée libère une place…
        var mostRecent = (await ListAsync(householdId))[0];
        await RevokeAsync(alice, householdId, mostRecent.Id);
        Assert.True((await CreateAsync(alice, householdId)).IsSuccess);

        // …tout comme une invitation expirée (la première, créée 8 jours plus tôt).
        Clock.Advance(TimeSpan.FromDays(2));
        Assert.DoesNotContain(await ListAsync(householdId), i => i.Id == first.Id);
        Assert.True((await CreateAsync(alice, householdId)).IsSuccess);
    }

    [Fact]
    public async Task Create_ConcurrentRequests_NeverExceedTheLimit()
    {
        var alice = await CreateUserAsync("Alice");
        var householdId = await CreateHouseholdAsync(alice);
        for (var i = 0; i < InvitationService.MaxActivePerHousehold - 1; i++)
        {
            await CreateAsync(alice, householdId);
        }

        // Une seule place libre, deux demandes simultanées.
        var results = await Task.WhenAll(CreateAsync(alice, householdId), CreateAsync(alice, householdId));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Equal(InvitationService.MaxActivePerHousehold, (await ListAsync(householdId)).Count);
    }

    // ---------- Consultation ----------

    [Fact]
    public async Task ListActive_ExcludesRevokedAndExpiredInvitations()
    {
        var alice = await CreateUserAsync("Alice");
        var householdId = await CreateHouseholdAsync(alice);
        await CreateAsync(alice, householdId);
        Clock.Advance(TimeSpan.FromDays(6));
        var revoked = (await CreateAsync(alice, householdId)).Value!;
        var active = (await CreateAsync(alice, householdId)).Value!;
        await RevokeAsync(alice, householdId, revoked.Id);
        Clock.Advance(TimeSpan.FromDays(2)); // la première invitation a maintenant expiré

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
