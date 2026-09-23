using Api.Common;
using Api.Data;
using Api.Dtos.Profiles;
using Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Profiles;

public static class ProfileErrors
{
    public static readonly Error NoProfile = new(
        ErrorType.NotFound, "profile.none", "Tu n'as pas encore renseigné tes préférences.");

    public static readonly Error ConsentRequired = new(
        ErrorType.Validation,
        "profile.health_consent_required",
        "Pour enregistrer tes allergies, coche la case de consentement.",
        new Dictionary<string, string[]>
        {
            [nameof(SaveProfileRequest.HealthDataConsent)] =
                ["Pour enregistrer tes allergies, coche la case de consentement."],
        });

    public static Error UnknownValue(string field) => new(
        ErrorType.Validation,
        "profile.validation",
        "Une des valeurs choisies n'existe pas.",
        new Dictionary<string, string[]> { [field] = ["Valeur inconnue."] });
}

public interface IProfileService
{
    Task<Result<ProfileDto>> GetAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Crée le profil (fin de l'onboarding) ou le remplace entièrement.
    /// </summary>
    Task<Result<ProfileDto>> SaveAsync(Guid userId, SaveProfileRequest request, CancellationToken ct);
}

public sealed class ProfileService(AppDbContext db, TimeProvider time) : IProfileService
{
    public async Task<Result<ProfileDto>> GetAsync(Guid userId, CancellationToken ct)
    {
        var profile = await db.UserProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, ct);
        return profile is null ? ProfileErrors.NoProfile : ToDto(profile);
    }

    public async Task<Result<ProfileDto>> SaveAsync(Guid userId, SaveProfileRequest request, CancellationToken ct)
    {
        var invalid = Validate(request);
        if (invalid is not null)
        {
            return invalid;
        }

        var now = time.GetUtcNow();
        var profile = await db.UserProfiles.SingleOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null)
        {
            profile = new UserProfile { UserId = userId, CreatedAt = now };
            db.UserProfiles.Add(profile);
        }

        profile.CookingTime = request.CookingTime;
        profile.Budget = request.Budget;
        profile.Diet = request.Diet;
        // Distinct + tri : pas de doublon, et un ordre stable (comparaisons, prompt IA).
        profile.Exclusions = request.Exclusions.Distinct().Order().ToList();
        profile.Goal = request.Goal;
        profile.DefaultServings = request.DefaultServings;

        if (request.HealthDataConsent)
        {
            profile.Allergens = request.Allergens.Distinct().Order().ToList();
            // On garde la date du PREMIER consentement (preuve), sans la réécrire à chaque modification.
            profile.HealthDataConsentAt ??= now;
        }
        else
        {
            // Pas (ou plus) de consentement : aucune donnée de santé conservée.
            profile.Allergens = [];
            profile.HealthDataConsentAt = null;
        }

        profile.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return ToDto(profile);
    }

    private static Error? Validate(SaveProfileRequest request)
    {
        // Allergies sans consentement : on refuse plutôt que d'ignorer en silence,
        // pour que l'utilisateur sache qu'elles ne seraient pas prises en compte.
        if (request.Allergens.Count > 0 && !request.HealthDataConsent)
        {
            return ProfileErrors.ConsentRequired;
        }

        // Garde-fou : le JSON n'accepte que des noms d'énumération connus, mais on vérifie
        // quand même ici (le service peut être appelé d'ailleurs, ex. des tests ou un import).
        if (!Enum.IsDefined(request.CookingTime)) return ProfileErrors.UnknownValue(nameof(request.CookingTime));
        if (!Enum.IsDefined(request.Budget)) return ProfileErrors.UnknownValue(nameof(request.Budget));
        if (!Enum.IsDefined(request.Diet)) return ProfileErrors.UnknownValue(nameof(request.Diet));
        if (!Enum.IsDefined(request.Goal)) return ProfileErrors.UnknownValue(nameof(request.Goal));
        if (!request.Exclusions.All(Enum.IsDefined)) return ProfileErrors.UnknownValue(nameof(request.Exclusions));
        if (!request.Allergens.All(Enum.IsDefined)) return ProfileErrors.UnknownValue(nameof(request.Allergens));
        return null;
    }

    private static ProfileDto ToDto(UserProfile p) => new(
        p.CookingTime,
        p.Budget,
        p.Diet,
        p.Exclusions,
        p.Allergens,
        p.HealthDataConsentAt is not null,
        p.Goal,
        p.DefaultServings);
}
