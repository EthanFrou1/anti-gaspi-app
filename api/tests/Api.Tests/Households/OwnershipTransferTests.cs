using Api.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Households;

/// <summary>
/// Règle de propriété du foyer (CLAUDE.md) : si le propriétaire quitte le foyer ou
/// supprime son compte, la propriété passe au membre le plus ancien ; s'il ne reste
/// aucun membre, le foyer et son contenu sont supprimés.
/// </summary>
public class OwnershipTransferTests(DatabaseFixture database) : HouseholdTestBase(database)
{
    // ---------- Le propriétaire quitte le foyer ----------

    [Fact]
    public async Task OwnerLeaves_OldestRemainingMemberBecomesOwner()
    {
        var (owner, bob, carol, householdId) = await CreateHouseholdWithThreeMembersAsync();

        var result = await RemoveMemberAsync(owner, householdId, owner);

        Assert.True(result.IsSuccess);
        var members = await GetMembersAsync(householdId);
        Assert.Equal(2, members.Count);
        Assert.Equal(HouseholdRole.Owner, members[bob]);
        Assert.Equal(HouseholdRole.Member, members[carol]);
    }

    [Fact]
    public async Task OwnerLeaves_SeniorityIsBasedOnJoinDateNotOnCreationOrder()
    {
        // Carol crée son compte avant Bob, mais rejoint le foyer après lui.
        var owner = await CreateUserAsync("Owner");
        var carol = await CreateUserAsync("Carol");
        var bob = await CreateUserAsync("Bob");
        var householdId = await CreateHouseholdAsync(owner);
        await AddMemberAsync(owner, householdId, bob);
        await AddMemberAsync(owner, householdId, carol);

        await RemoveMemberAsync(owner, householdId, owner);

        var members = await GetMembersAsync(householdId);
        Assert.Equal(HouseholdRole.Owner, members[bob]);
        Assert.Equal(HouseholdRole.Member, members[carol]);
    }

    [Fact]
    public async Task OwnerLeaves_WhenAlone_DeletesHouseholdAndItsContent()
    {
        var owner = await CreateUserAsync("Owner");
        var householdId = await CreateHouseholdAsync(owner);
        await CreateInvitationCodeAsync(owner, householdId);

        var result = await RemoveMemberAsync(owner, householdId, owner);

        Assert.True(result.IsSuccess);
        Assert.False(await HouseholdExistsAsync(householdId));
        await using var db = CreateDbContext();
        Assert.False(await db.HouseholdInvitations.AnyAsync(i => i.HouseholdId == householdId));
        Assert.False(await db.HouseholdMembers.AnyAsync(m => m.HouseholdId == householdId));
    }

    [Fact]
    public async Task LastMemberLeaves_AfterOwnerLeft_DeletesHousehold()
    {
        var owner = await CreateUserAsync("Owner");
        var bob = await CreateUserAsync("Bob");
        var householdId = await CreateHouseholdAsync(owner);
        await AddMemberAsync(owner, householdId, bob);

        await RemoveMemberAsync(owner, householdId, owner);
        await RemoveMemberAsync(bob, householdId, bob);

        Assert.False(await HouseholdExistsAsync(householdId));
    }

    [Fact]
    public async Task MemberLeaves_OwnerIsUnchanged()
    {
        var (owner, bob, carol, householdId) = await CreateHouseholdWithThreeMembersAsync();

        await RemoveMemberAsync(bob, householdId, bob);

        var members = await GetMembersAsync(householdId);
        Assert.Equal(HouseholdRole.Owner, members[owner]);
        Assert.Equal(HouseholdRole.Member, members[carol]);
        Assert.False(members.ContainsKey(bob));
    }

    // ---------- Le propriétaire supprime son compte ----------

    [Fact]
    public async Task OwnerDeletesAccount_OldestRemainingMemberBecomesOwner()
    {
        var (owner, bob, carol, householdId) = await CreateHouseholdWithThreeMembersAsync();

        var result = await DeleteAccountAsync(owner);

        Assert.True(result.IsSuccess);
        var members = await GetMembersAsync(householdId);
        Assert.Equal(2, members.Count);
        Assert.Equal(HouseholdRole.Owner, members[bob]);
        Assert.Equal(HouseholdRole.Member, members[carol]);

        await using var db = CreateDbContext();
        Assert.False(await db.Users.AnyAsync(u => u.Id == owner));
    }

    [Fact]
    public async Task OwnerDeletesAccount_WhenAlone_DeletesHouseholdAndItsContent()
    {
        var owner = await CreateUserAsync("Owner");
        var householdId = await CreateHouseholdAsync(owner);
        await CreateInvitationCodeAsync(owner, householdId);

        var result = await DeleteAccountAsync(owner);

        Assert.True(result.IsSuccess);
        Assert.False(await HouseholdExistsAsync(householdId));
        await using var db = CreateDbContext();
        Assert.False(await db.HouseholdInvitations.AnyAsync(i => i.HouseholdId == householdId));
    }

    [Fact]
    public async Task MemberDeletesAccount_OwnerIsUnchanged()
    {
        var (owner, bob, carol, householdId) = await CreateHouseholdWithThreeMembersAsync();

        await DeleteAccountAsync(carol);

        var members = await GetMembersAsync(householdId);
        Assert.Equal(HouseholdRole.Owner, members[owner]);
        Assert.Equal(HouseholdRole.Member, members[bob]);
        Assert.False(members.ContainsKey(carol));
    }

    [Fact]
    public async Task OwnerDeletesAccount_InvitationsHeCreatedRemainUsable()
    {
        var owner = await CreateUserAsync("Owner");
        var bob = await CreateUserAsync("Bob");
        var dave = await CreateUserAsync("Dave");
        var householdId = await CreateHouseholdAsync(owner);
        await AddMemberAsync(owner, householdId, bob);
        // Le code actif, créé par le propriétaire pour faire entrer Bob.
        var code = await ActiveInvitationCodeAsync(owner, householdId);

        await DeleteAccountAsync(owner);
        var join = await JoinAsync(dave, code);

        Assert.True(join.IsSuccess);
        Assert.Equal(householdId, join.Value.Id);
    }

    // ---------- Concurrence ----------

    [Fact]
    public async Task OwnerAndLastMemberLeaveSimultaneously_HouseholdIsDeleted()
    {
        var owner = await CreateUserAsync("Owner");
        var bob = await CreateUserAsync("Bob");
        var householdId = await CreateHouseholdAsync(owner);
        await AddMemberAsync(owner, householdId, bob);

        // Sans verrou sur le foyer, chacun pourrait voir l'autre encore présent
        // et le foyer resterait vide sans être supprimé.
        await Task.WhenAll(
            RemoveMemberAsync(owner, householdId, owner),
            RemoveMemberAsync(bob, householdId, bob));

        Assert.False(await HouseholdExistsAsync(householdId));
    }

    private async Task<(Guid Owner, Guid Bob, Guid Carol, Guid HouseholdId)> CreateHouseholdWithThreeMembersAsync()
    {
        var owner = await CreateUserAsync("Owner");
        var bob = await CreateUserAsync("Bob");
        var carol = await CreateUserAsync("Carol");
        var householdId = await CreateHouseholdAsync(owner);
        await AddMemberAsync(owner, householdId, bob);   // arrivé en premier → le plus ancien
        await AddMemberAsync(owner, householdId, carol);
        return (owner, bob, carol, householdId);
    }
}
