using Api.Common;
using Api.Data;
using Api.Dtos.Auth;
using Api.Entities;
using Api.Options;
using Api.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.Auth;

public sealed class AuthService(
    AppDbContext db,
    UserManager<User> userManager,
    ITokenService tokenService,
    IUserService userService,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider time,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        var user = new User
        {
            // Identity exige un UserName : on utilise l'email, ce qui rend aussi
            // l'email unique grâce à l'index unique d'Identity sur le UserName.
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return MapIdentityErrors(result.Errors);
        }

        return await IssueTokensAsync(user, ct);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return AuthErrors.InvalidCredentials;
        }

        // Verrouillage après plusieurs échecs (réglé dans AddAppIdentity) :
        // freine les attaques par force brute sur un compte précis.
        if (await userManager.IsLockedOutAsync(user))
        {
            return AuthErrors.LockedOut;
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            return AuthErrors.InvalidCredentials;
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return await IssueTokensAsync(user, ct);
    }

    public async Task<Result<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var hash = tokenService.HashRefreshToken(refreshToken);

        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        if (stored.RevokedAt is not null)
        {
            // Un jeton déjà échangé contre un nouveau est présenté à nouveau :
            // quelqu'un d'autre en possède probablement une copie (vol).
            // On révoque toutes les sessions de l'utilisateur par précaution.
            if (stored.ReplacedByTokenId is not null)
            {
                logger.LogWarning(
                    "Réutilisation d'un refresh token détectée pour l'utilisateur {UserId} : révocation de toutes ses sessions.",
                    stored.UserId);
                await RevokeAllAsync(stored.UserId, now, ct);
            }

            return AuthErrors.InvalidRefreshToken;
        }

        if (stored.ExpiresAt <= now)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        // Rotation : l'ancien jeton est révoqué et pointe vers son remplaçant.
        var (rawToken, newToken) = BuildRefreshToken(stored.UserId, now);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Mise à jour conditionnelle (« seulement s'il n'est pas déjà révoqué ») exécutée
        // en une seule requête SQL. Si deux requêtes utilisent le même jeton en même
        // temps, PostgreSQL les sérialise : une seule met à jour la ligne, l'autre
        // obtient 0 ligne et échoue. Sans ça, on pourrait créer deux sessions valides.
        var revoked = await db.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.RevokedAt, now)
                      .SetProperty(t => t.ReplacedByTokenId, newToken.Id),
                ct);

        if (revoked == 0)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        db.RefreshTokens.Add(newToken);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await BuildResponseAsync(stored.User, rawToken, newToken, ct);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var hash = tokenService.HashRefreshToken(refreshToken);
        var now = time.GetUtcNow();

        // Pas d'erreur si le jeton est inconnu : la déconnexion est idempotente
        // et on ne donne aucune information sur la validité du jeton.
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private async Task<Result<AuthResponse>> IssueTokensAsync(User user, CancellationToken ct)
    {
        var (rawToken, refreshToken) = BuildRefreshToken(user.Id, time.GetUtcNow());
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync(ct);

        return await BuildResponseAsync(user, rawToken, refreshToken, ct);
    }

    private (string RawToken, RefreshToken Entity) BuildRefreshToken(Guid userId, DateTimeOffset now)
    {
        var generated = tokenService.CreateRefreshToken();
        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = generated.Hash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(jwtOptions.Value.RefreshTokenDays),
        };
        return (generated.RawToken, entity);
    }

    private async Task<Result<AuthResponse>> BuildResponseAsync(
        User user, string rawRefreshToken, RefreshToken refreshToken, CancellationToken ct)
    {
        var userDto = await userService.GetAsync(user.Id, ct);
        if (!userDto.IsSuccess)
        {
            return userDto.Error;
        }

        var accessToken = tokenService.CreateAccessToken(user);
        return new AuthResponse(
            accessToken.Token,
            accessToken.ExpiresAt,
            rawRefreshToken,
            refreshToken.ExpiresAt,
            userDto.Value);
    }

    private Task<int> RevokeAllAsync(Guid userId, DateTimeOffset now, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

    private static Error MapIdentityErrors(IEnumerable<IdentityError> errors)
    {
        var list = errors.ToList();

        if (list.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail)
                                 or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return AuthErrors.EmailAlreadyUsed;
        }

        // Regroupe les erreurs par champ pour que l'app puisse les afficher sous le bon input.
        var byField = list
            .GroupBy(e => FieldForIdentityError(e.Code))
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

        return new Error(ErrorType.Validation, "auth.validation", "Les informations saisies sont invalides.", byField);
    }

    // Mêmes noms de champs que la validation automatique d'ASP.NET (nom de la propriété du DTO),
    // pour que l'app mobile n'ait qu'une seule convention à gérer.
    private static string FieldForIdentityError(string code)
    {
        if (code.StartsWith("Password", StringComparison.Ordinal))
        {
            return nameof(RegisterRequest.Password);
        }

        if (code.Contains("Email", StringComparison.Ordinal) || code.Contains("UserName", StringComparison.Ordinal))
        {
            return nameof(RegisterRequest.Email);
        }

        return string.Empty;
    }
}
