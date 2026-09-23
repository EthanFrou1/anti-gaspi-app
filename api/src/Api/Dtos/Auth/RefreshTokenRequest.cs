using System.ComponentModel.DataAnnotations;

namespace Api.Dtos.Auth;

// Utilisé par /refresh et /logout.
public sealed record RefreshTokenRequest(
    [Required, StringLength(200)]
    string RefreshToken);
