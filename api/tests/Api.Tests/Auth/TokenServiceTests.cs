using Api.Entities;
using Api.Services.Auth;
using Api.Tests.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Api.Tests.Auth;

// Tests purement unitaires : aucune base de données nécessaire.
public class TokenServiceTests
{
    private static readonly TestClock Clock = new(DateTimeOffset.UtcNow);

    private readonly TokenService _service = new(
        Microsoft.Extensions.Options.Options.Create(DatabaseTestBase.TestJwtOptions), Clock);

    private readonly User _user = new() { Id = Guid.NewGuid(), DisplayName = "Alice" };

    [Fact]
    public async Task CreateAccessToken_ProducesValidTokenContainingUserId()
    {
        var accessToken = _service.CreateAccessToken(_user);

        var result = await ValidateAsync(accessToken.Token, DatabaseTestBase.TestJwtOptions.SigningKey);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(_user.Id.ToString(), result.Claims[JwtRegisteredClaimNames.Sub]);
    }

    [Fact]
    public void CreateAccessToken_ExpiresAfterConfiguredDuration()
    {
        var accessToken = _service.CreateAccessToken(_user);

        Assert.Equal(Clock.Now.AddMinutes(15), accessToken.ExpiresAt);
    }

    [Fact]
    public async Task CreateAccessToken_IsRejectedWithAnotherSigningKey()
    {
        var accessToken = _service.CreateAccessToken(_user);

        var result = await ValidateAsync(accessToken.Token, "une-autre-clé-tout-aussi-longue-que-la-vraie!!");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateRefreshToken_ReturnsRandomTokenWithMatchingHash()
    {
        var first = _service.CreateRefreshToken();
        var second = _service.CreateRefreshToken();

        Assert.NotEqual(first.RawToken, second.RawToken);
        Assert.NotEqual(first.RawToken, first.Hash);
        Assert.Equal(first.Hash, _service.HashRefreshToken(first.RawToken));
    }

    private static Task<TokenValidationResult> ValidateAsync(string token, string signingKey) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = DatabaseTestBase.TestJwtOptions.Issuer,
            ValidAudience = DatabaseTestBase.TestJwtOptions.Audience,
            IssuerSigningKey = JwtSigningKey.Create(signingKey),
            ValidAlgorithms = [JwtSigningKey.Algorithm],
        });
}
