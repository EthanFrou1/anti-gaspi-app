using System.ComponentModel.DataAnnotations;

namespace Api.Dtos.Households;

public sealed record JoinHouseholdRequest(
    [Required(ErrorMessage = "Le code d'invitation est obligatoire.")]
    [StringLength(20, ErrorMessage = "Le code d'invitation n'est pas valide.")]
    string Code);
