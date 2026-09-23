using System.ComponentModel.DataAnnotations;

namespace Api.Dtos.Users;

// Le mot de passe est redemandé : un access token volé ne suffit pas à supprimer le compte.
public sealed record DeleteAccountRequest(
    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [StringLength(128)]
    string Password);
