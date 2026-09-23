using System.ComponentModel.DataAnnotations;

namespace Api.Dtos.Auth;

public sealed record LoginRequest(
    [Required(ErrorMessage = "L'email est obligatoire.")]
    [StringLength(254)]
    string Email,

    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [StringLength(128)]
    string Password);
