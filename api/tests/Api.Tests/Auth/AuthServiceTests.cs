using Api.Common;
using Api.Dtos.Auth;
using Api.Services.Auth;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Auth;

public class AuthServiceTests(DatabaseFixture database) : DatabaseTestBase(database)
{
    private const string Email = "alice@example.com";
    private const string Password = "un-mot-de-passe-solide";

    // ---------- Inscription ----------

    [Fact]
    public async Task Register_CreatesUserAndReturnsTokens()
    {
        var result = await RegisterAsync();

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.Value.AccessToken));
        Assert.False(string.IsNullOrEmpty(result.Value.RefreshToken));
        Assert.Equal(Email, result.Value.User.Email);
        Assert.Equal("Alice", result.Value.User.DisplayName);
        Assert.Null(result.Value.User.HouseholdId);
    }

    [Fact]
    public async Task Register_StoresOnlyTheHashOfTheRefreshToken()
    {
        var result = await RegisterAsync();

        await using var db = CreateDbContext();
        var stored = await db.RefreshTokens.SingleAsync();
        Assert.NotEqual(result.Value!.RefreshToken, stored.TokenHash);
    }

    [Fact]
    public async Task Register_WithExistingEmail_IsRejectedRegardlessOfCase()
    {
        await RegisterAsync();

        var result = await RegisterAsync(email: "ALICE@example.com");

        Assert.Equal(AuthErrors.EmailAlreadyUsed, result.Error);
    }

    [Fact]
    public async Task Register_WithTooShortPassword_ReturnsValidationErrorOnPasswordField()
    {
        var result = await RegisterAsync(password: "court");

        Assert.Equal(ErrorType.Validation, result.Error?.Type);
        Assert.Contains(nameof(RegisterRequest.Password), result.Error!.ValidationErrors!.Keys);
    }

    // ---------- Connexion ----------

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsTokens()
    {
        await RegisterAsync();

        var result = await LoginAsync(Password);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsInvalidCredentials()
    {
        await RegisterAsync();

        var result = await LoginAsync("mauvais-mot-de-passe");

        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsSameErrorAsWrongPassword()
    {
        var result = await WithServiceAsync<IAuthService, Result<AuthResponse>>(
            s => s.LoginAsync(new LoginRequest("inconnu@example.com", Password), CancellationToken.None));

        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task Login_AfterFiveFailures_IsLockedOutEvenWithCorrectPassword()
    {
        await RegisterAsync();
        for (var i = 0; i < 5; i++)
        {
            await LoginAsync("mauvais-mot-de-passe");
        }

        var result = await LoginAsync(Password);

        Assert.Equal(AuthErrors.LockedOut, result.Error);
    }

    // ---------- Rafraîchissement ----------

    [Fact]
    public async Task Refresh_RotatesTheToken()
    {
        var initial = (await RegisterAsync()).Value!;

        var refreshed = await RefreshAsync(initial.RefreshToken);

        Assert.True(refreshed.IsSuccess);
        Assert.NotEqual(initial.RefreshToken, refreshed.Value.RefreshToken);

        // L'ancien jeton est révoqué et pointe vers son remplaçant.
        await using var db = CreateDbContext();
        var tokens = await db.RefreshTokens.ToListAsync();
        Assert.Equal(2, tokens.Count);
        var oldToken = Assert.Single(tokens, t => t.ReplacedByTokenId is not null);
        var newToken = Assert.Single(tokens, t => t.Id == oldToken.ReplacedByTokenId);
        Assert.NotNull(oldToken.RevokedAt);
        Assert.Null(newToken.RevokedAt);
    }

    [Fact]
    public async Task Refresh_WithNewToken_WorksAgain()
    {
        var initial = (await RegisterAsync()).Value!;
        var refreshed = (await RefreshAsync(initial.RefreshToken)).Value!;

        var again = await RefreshAsync(refreshed.RefreshToken);

        Assert.True(again.IsSuccess);
    }

    [Fact]
    public async Task Refresh_ReusingAnAlreadyRotatedToken_RevokesAllSessions()
    {
        var initial = (await RegisterAsync()).Value!;
        var otherSession = (await LoginAsync(Password)).Value!;
        var rotated = (await RefreshAsync(initial.RefreshToken)).Value!;

        // Un attaquant rejoue l'ancien jeton.
        var replay = await RefreshAsync(initial.RefreshToken);

        Assert.Equal(AuthErrors.InvalidRefreshToken, replay.Error);
        // Par précaution, toutes les sessions de l'utilisateur sont coupées.
        Assert.Equal(AuthErrors.InvalidRefreshToken, (await RefreshAsync(rotated.RefreshToken)).Error);
        Assert.Equal(AuthErrors.InvalidRefreshToken, (await RefreshAsync(otherSession.RefreshToken)).Error);
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_IsRejected()
    {
        var initial = (await RegisterAsync()).Value!;

        Clock.Advance(TimeSpan.FromDays(31));
        var result = await RefreshAsync(initial.RefreshToken);

        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_IsRejected()
    {
        var result = await RefreshAsync("jeton-inventé");

        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
    }

    [Fact]
    public async Task Refresh_ConcurrentCallsWithSameToken_OnlyOneSucceeds()
    {
        var initial = (await RegisterAsync()).Value!;

        // Deux requêtes simultanées avec le même jeton (chacune dans son propre scope,
        // comme deux requêtes HTTP distinctes).
        var results = await Task.WhenAll(
            RefreshAsync(initial.RefreshToken),
            RefreshAsync(initial.RefreshToken));

        Assert.Single(results, r => r.IsSuccess);
    }

    // ---------- Déconnexion ----------

    [Fact]
    public async Task Logout_RevokesTheRefreshToken()
    {
        var initial = (await RegisterAsync()).Value!;

        await WithServiceAsync<IAuthService>(s => s.LogoutAsync(initial.RefreshToken, CancellationToken.None));

        Assert.Equal(AuthErrors.InvalidRefreshToken, (await RefreshAsync(initial.RefreshToken)).Error);
    }

    [Fact]
    public async Task Logout_DoesNotAffectOtherSessions()
    {
        var first = (await RegisterAsync()).Value!;
        var second = (await LoginAsync(Password)).Value!;

        await WithServiceAsync<IAuthService>(s => s.LogoutAsync(first.RefreshToken, CancellationToken.None));

        Assert.True((await RefreshAsync(second.RefreshToken)).IsSuccess);
    }

    // ---------- Helpers ----------

    private Task<Result<AuthResponse>> RegisterAsync(string email = Email, string password = Password) =>
        WithServiceAsync<IAuthService, Result<AuthResponse>>(
            s => s.RegisterAsync(new RegisterRequest(email, password, "Alice"), CancellationToken.None));

    private Task<Result<AuthResponse>> LoginAsync(string password) =>
        WithServiceAsync<IAuthService, Result<AuthResponse>>(
            s => s.LoginAsync(new LoginRequest(Email, password), CancellationToken.None));

    private Task<Result<AuthResponse>> RefreshAsync(string refreshToken) =>
        WithServiceAsync<IAuthService, Result<AuthResponse>>(
            s => s.RefreshAsync(refreshToken, CancellationToken.None));
}
