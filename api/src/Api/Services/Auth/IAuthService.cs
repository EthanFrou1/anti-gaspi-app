using Api.Common;
using Api.Dtos.Auth;

namespace Api.Services.Auth;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct);

    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct);

    Task<Result<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken ct);

    Task LogoutAsync(string refreshToken, CancellationToken ct);
}
