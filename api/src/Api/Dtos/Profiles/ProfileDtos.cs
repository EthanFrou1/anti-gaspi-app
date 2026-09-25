using System.ComponentModel.DataAnnotations;
using Api.Entities;

namespace Api.Dtos.Profiles;

public sealed record ProfileDto(
    CookingTime CookingTime,
    MealBudget Budget,
    Diet Diet,
    IReadOnlyList<IngredientExclusion> Exclusions,
    IReadOnlyList<Allergen> Allergens,
    // true si l'utilisateur a consenti au stockage de ses allergies (donnée de santé).
    bool HealthDataConsent,
    NutritionGoal Goal,
    int DefaultServings,
    IReadOnlyList<DislikedFood> Dislikes,
    bool AvoidSpicy);

/// <summary>
/// Profil complet, à la création (fin de l'onboarding) comme à la modification.
/// </summary>
public sealed record SaveProfileRequest(
    CookingTime CookingTime,
    MealBudget Budget,
    Diet Diet,
    [Required] IReadOnlyList<IngredientExclusion> Exclusions,
    [Required] IReadOnlyList<Allergen> Allergens,
    // Consentement explicite exigé pour enregistrer des allergies (RGPD, article 9).
    bool HealthDataConsent,
    NutritionGoal Goal,
    [Range(1, 12, ErrorMessage = "Le nombre de portions doit être compris entre 1 et 12.")]
    int DefaultServings,
    // Facultatifs : l'onboarding ne les demande pas (proposés avant la première recette).
    IReadOnlyList<DislikedFood>? Dislikes = null,
    bool AvoidSpicy = false);
