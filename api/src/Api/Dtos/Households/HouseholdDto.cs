using Api.Entities;

namespace Api.Dtos.Households;

public sealed record HouseholdDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    HouseholdRole MyRole,
    IReadOnlyList<HouseholdMemberDto> Members);

// Pas d'email : les autres membres n'en ont pas besoin (minimisation des données).
public sealed record HouseholdMemberDto(
    Guid UserId,
    string DisplayName,
    HouseholdRole Role,
    DateTimeOffset JoinedAt);
