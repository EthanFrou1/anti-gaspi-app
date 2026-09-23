using System.ComponentModel.DataAnnotations;

namespace Api.Dtos.Households;

public sealed record CreateHouseholdRequest(
    [Required(ErrorMessage = "Le nom du foyer est obligatoire.")]
    [StringLength(50, ErrorMessage = "Le nom du foyer ne doit pas dépasser 50 caractères.")]
    string Name);
