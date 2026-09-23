using Api.Entities;

namespace Api.Services.Auth;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

// RawToken est envoyé au client ; seul Hash est enregistré en base.
public sealed record GeneratedRefreshToken(string RawToken, string Hash);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    GeneratedRefreshToken CreateRefreshToken();

    string HashRefreshToken(string rawToken);
}
