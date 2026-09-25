using Api.Common;
using Api.Dtos.Households;
using Api.Entities;
using Api.Services.Households;
using Api.Services.Users;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Households;

public class HouseholdServiceTests(DatabaseFixture database) : HouseholdTestBase(database)
{
    // ---------- Création ----------

    [Fact]
    public async Task Create_MakesTheCreatorOwner()
    {
        var alice = await CreateUserAsync("Alice");

        var result = await CreateHouseholdResultAsync(alice, "  Appart Lyon  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Appart Lyon", result.Value.Name);
        Assert.Equal(HouseholdRole.Owner, result.Value.MyRole);
        var member = Assert.Single(result.Value.Members);
        Assert.Equal(alice, member.UserId);
        Assert.Equal(HouseholdRole.Owner, member.Role);
    }

    [Fact]
    public async Task Create_WhenAlreadyInAHousehold_ReturnsConflict()
    {
        var alice = await CreateUserAsync("Alice");
        await CreateHouseholdAsync(alice);

        var result = await CreateHouseholdResultAsync(alice, "Deuxième foyer");

        Assert.Equal(HouseholdErrors.AlreadyMember, result.Error);
    }

    [Fact]
    public async Task Create_IsVisibleInUserProfile()
    {
        var alice = await CreateUserAsync("Alice");
        var householdId = await CreateHouseholdAsync(alice);

        var me = await WithServiceAsync<IUserService, Result<Api.Dtos.Users.UserDto>>(s =>
            s.GetAsync(alice, CancellationToken.None));

        Assert.Equal(householdId, me.Value!.HouseholdId);
    }

    // ---------- Consultation ----------

    [Fact]
    public async Task GetMine_WithoutHousehold_ReturnsNoHousehold()
    {
        var alice = await CreateUserAsync("Alice");

        var result = await WithServiceAsync<IHouseholdService, Result<HouseholdDto>>(s =>
            s.GetMineAsync(alice, CancellationToken.None));

        Assert.Equal(HouseholdErrors.NoHousehold, result.Error);
    }

    [Fact]
    public async Task GetMine_ListsMembersByJoinDate()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var householdId = await CreateHouseholdAsync(alice);
        await AddMemberAsync(alice, householdId, bob);

        var result = await WithServiceAsync<IHouseholdService, Result<HouseholdDto>>(s =>
            s.GetMineAsync(bob, CancellationToken.None));

        Assert.Equal(HouseholdRole.Member, result.Value!.MyRole);
        Assert.Equal(["Alice", "Bob"], result.Value.Members.Select(m => m.DisplayName));
    }

    // ---------- Rejoindre ----------

    [Fact]
    public async Task Join_WithValidCode_AddsMember()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var householdId = await CreateHouseholdAsync(alice);
        var code = await CreateInvitationCodeAsync(alice, householdId);

        var result = await JoinAsync(bob, code);

        Assert.True(result.IsSuccess);
        Assert.Equal(householdId, result.Value.Id);
        Assert.Equal(HouseholdRole.Member, result.Value.MyRole);
    }

    [Fact]
    public async Task Join_AcceptsCodeTypedInLowercaseWithDash()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var householdId = await CreateHouseholdAsync(alice);
        var code = await CreateInvitationCodeAsync(alice, householdId);
        var typed = $" {code[..4].ToLowerInvariant()}-{code[4..].ToLowerInvariant()} ";

        var result = await JoinAsync(bob, typed);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Join_WithUnknownCode_Fails()
    {
        var bob = await CreateUserAsync("Bob");

        var result = await JoinAsync(bob, "ZZZZZZZZ");

        Assert.Equal(HouseholdErrors.InvitationInvalid, result.Error);
    }

    [Fact]
    public async Task Join_WithExpiredCode_Fails()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var householdId = await CreateHouseholdAsync(alice);
        var code = await CreateInvitationCodeAsync(alice, householdId);

        Clock.Advance(InvitationService.Validity + TimeSpan.FromMinutes(1));
        var result = await JoinAsync(bob, code);

        Assert.Equal(HouseholdErrors.InvitationInvalid, result.Error);
    }

    [Fact]
    public async Task Join_WithRevokedCode_Fails()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var householdId = await CreateHouseholdAsync(alice);
        var invitation = (await WithServiceAsync<IInvitationService, Result<InvitationDto>>(s =>
            s.CreateAsync(alice, householdId, CancellationToken.None))).Value!;
        await WithServiceAsync<IInvitationService, Result>(s =>
            s.RevokeAsync(alice, householdId, invitation.Id, CancellationToken.None));

        var result = await JoinAsync(bob, invitation.Code);

        Assert.Equal(HouseholdErrors.InvitationInvalid, result.Error);
    }

    [Fact]
    public async Task Join_WhenAlreadyInAHousehold_ReturnsConflict()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var aliceHousehold = await CreateHouseholdAsync(alice);
        await CreateHouseholdAsync(bob, "Chez Bob");
        var code = await CreateInvitationCodeAsync(alice, aliceHousehold);

        var result = await JoinAsync(bob, code);

        Assert.Equal(HouseholdErrors.AlreadyMember, result.Error);
    }

    [Fact]
    public async Task Join_SameCodeCanBeUsedByMultiplePeople()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var carol = await CreateUserAsync("Carol");
        var householdId = await CreateHouseholdAsync(alice);
        var code = await CreateInvitationCodeAsync(alice, householdId);

        Assert.True((await JoinAsync(bob, code)).IsSuccess);
        Assert.True((await JoinAsync(carol, code)).IsSuccess);
    }

    // ---------- Retirer un membre ----------

    [Fact]
    public async Task Remove_OwnerCanRemoveAMember()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var householdId = await CreateHouseholdAsync(alice);
        await AddMemberAsync(alice, householdId, bob);

        var result = await RemoveMemberAsync(alice, householdId, bob);

        Assert.True(result.IsSuccess);
        Assert.False((await GetMembersAsync(householdId)).ContainsKey(bob));
    }

    [Fact]
    public async Task Remove_ExcludedMemberCannotComeBackWithAKnownCode()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var householdId = await CreateHouseholdAsync(alice);
        // Bob connaît deux codes : celui qui l'a fait entrer, puis celui qu'il a créé lui-même
        // après la révocation du premier (un seul code actif à la fois).
        var invitation = (await WithServiceAsync<IInvitationService, Result<InvitationDto>>(s =>
            s.CreateAsync(alice, householdId, CancellationToken.None))).Value!;
        var code = invitation.Code;
        Assert.True((await JoinAsync(bob, code)).IsSuccess);
        await WithServiceAsync<IInvitationService, Result>(s =>
            s.RevokeAsync(alice, householdId, invitation.Id, CancellationToken.None));
        var bobCode = await CreateInvitationCodeAsync(bob, householdId);

        await RemoveMemberAsync(alice, householdId, bob);

        Assert.Equal(HouseholdErrors.InvitationInvalid, (await JoinAsync(bob, code)).Error);
        Assert.Equal(HouseholdErrors.InvitationInvalid, (await JoinAsync(bob, bobCode)).Error);
    }

    [Fact]
    public async Task Leave_Voluntarily_KeepsInvitationsActive()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var carol = await CreateUserAsync("Carol");
        var householdId = await CreateHouseholdAsync(alice);
        await AddMemberAsync(alice, householdId, bob);
        var code = await ActiveInvitationCodeAsync(alice, householdId);

        await RemoveMemberAsync(bob, householdId, bob);

        Assert.True((await JoinAsync(carol, code)).IsSuccess);
    }

    [Fact]
    public async Task Remove_MemberCannotRemoveSomeoneElse()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var carol = await CreateUserAsync("Carol");
        var householdId = await CreateHouseholdAsync(alice);
        await AddMemberAsync(alice, householdId, bob);
        await AddMemberAsync(alice, householdId, carol);

        var result = await RemoveMemberAsync(bob, householdId, carol);

        Assert.Equal(HouseholdErrors.OwnerOnly, result.Error);
        Assert.True((await GetMembersAsync(householdId)).ContainsKey(carol));
    }

    [Fact]
    public async Task Remove_ByNonMember_ReturnsNotFound()
    {
        var alice = await CreateUserAsync("Alice");
        var mallory = await CreateUserAsync("Mallory");
        var householdId = await CreateHouseholdAsync(alice);

        var result = await RemoveMemberAsync(mallory, householdId, alice);

        Assert.Equal(HouseholdErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Remove_UserNotInHousehold_ReturnsMemberNotFound()
    {
        var alice = await CreateUserAsync("Alice");
        var stranger = await CreateUserAsync("Stranger");
        var householdId = await CreateHouseholdAsync(alice);

        var result = await RemoveMemberAsync(alice, householdId, stranger);

        Assert.Equal(HouseholdErrors.MemberNotFound, result.Error);
    }

    [Fact]
    public async Task RemovedMember_CanCreateANewHousehold()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var householdId = await CreateHouseholdAsync(alice);
        await AddMemberAsync(alice, householdId, bob);
        await RemoveMemberAsync(alice, householdId, bob);

        var result = await CreateHouseholdResultAsync(bob, "Chez Bob");

        Assert.True(result.IsSuccess);
    }

    // ---------- Suppression de compte ----------

    [Fact]
    public async Task DeleteAccount_WithWrongPassword_KeepsTheAccount()
    {
        var alice = await CreateUserAsync("Alice");

        var result = await DeleteAccountAsync(alice, "mauvais-mot-de-passe");

        Assert.Equal(UserErrors.InvalidPassword, result.Error);
        await using var db = CreateDbContext();
        Assert.True(await db.Users.AnyAsync(u => u.Id == alice));
    }

    [Fact]
    public async Task DeleteAccount_RemovesUserAndRefreshTokens()
    {
        var alice = await CreateUserAsync("Alice");

        var result = await DeleteAccountAsync(alice);

        Assert.True(result.IsSuccess);
        await using var db = CreateDbContext();
        Assert.False(await db.Users.AnyAsync(u => u.Id == alice));
        Assert.False(await db.RefreshTokens.AnyAsync(t => t.UserId == alice));
    }
}
