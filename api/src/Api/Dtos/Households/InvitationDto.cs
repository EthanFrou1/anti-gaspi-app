namespace Api.Dtos.Households;

public sealed record InvitationDto(Guid Id, string Code, DateTimeOffset ExpiresAt);
