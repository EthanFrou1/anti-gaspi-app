using Api.Common;

namespace Api.Services.Households;

public static class HouseholdErrors
{
    // Utilisé aussi quand l'utilisateur n'est pas membre : on ne révèle pas
    // l'existence d'un foyer auquel il n'a pas accès.
    public static readonly Error NotFound = new(
        ErrorType.NotFound, "household.not_found", "Foyer introuvable.");

    public static readonly Error NoHousehold = new(
        ErrorType.NotFound, "household.none", "Tu n'as pas encore de foyer.");

    public static readonly Error AlreadyMember = new(
        ErrorType.Conflict, "household.already_member", "Tu fais déjà partie d'un foyer.");

    public static readonly Error OwnerOnly = new(
        ErrorType.Forbidden, "household.owner_only", "Seul le propriétaire du foyer peut faire ça.");

    public static readonly Error MemberNotFound = new(
        ErrorType.NotFound, "household.member_not_found", "Ce membre ne fait pas partie du foyer.");

    public static readonly Error InvitationInvalid = new(
        ErrorType.NotFound, "invitation.invalid", "Ce code d'invitation est invalide ou a expiré.");

    public static readonly Error InvitationNotFound = new(
        ErrorType.NotFound, "invitation.not_found", "Invitation introuvable.");

    public static readonly Error InvitationNotCreator = new(
        ErrorType.Forbidden, "invitation.not_creator", "Tu ne peux révoquer que les invitations que tu as créées.");

    public static readonly Error InvitationLimitReached = new(
        ErrorType.Conflict,
        "invitation.limit_reached",
        "Ce foyer a déjà 10 invitations actives. Révoque-en une avant d'en créer une nouvelle.");
}
