namespace Api.Entities;

/// <summary>
/// Foyer : c'est lui qui possède l'inventaire, pas l'utilisateur.
/// </summary>
public class Household
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<HouseholdMember> Members { get; set; } = [];

    public List<HouseholdInvitation> Invitations { get; set; } = [];
}
