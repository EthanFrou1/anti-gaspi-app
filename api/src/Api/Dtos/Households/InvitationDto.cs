namespace Api.Dtos.Households;

// CreatedByUserId permet à l'app de savoir quelles invitations l'utilisateur
// peut révoquer (les siennes, ou toutes s'il est propriétaire). Null si l'auteur
// a supprimé son compte.
public sealed record InvitationDto(Guid Id, string Code, DateTimeOffset ExpiresAt, Guid? CreatedByUserId);
