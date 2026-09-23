using System.ComponentModel.DataAnnotations;

namespace Api.Dtos.Auth;

public sealed record RegisterRequest(
    [Required(ErrorMessage = "L'email est obligatoire.")]
    [EmailAddress(ErrorMessage = "L'email n'est pas valide.")]
    [StringLength(254, ErrorMessage = "L'email est trop long.")]
    string Email,

    // La longueur minimale est vérifiée par Identity ; la maximale évite
    // de faire hacher des chaînes énormes (coût CPU).
    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [StringLength(128, ErrorMessage = "Le mot de passe est trop long.")]
    string Password,

    [Required(ErrorMessage = "Le prénom ou pseudo est obligatoire.")]
    [StringLength(50, ErrorMessage = "Le prénom ou pseudo ne doit pas dépasser 50 caractères.")]
    string DisplayName);
