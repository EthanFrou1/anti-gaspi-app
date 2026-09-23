namespace Api.Entities;

/// <summary>
/// Foyer : c'est lui qui possède l'inventaire, pas l'utilisateur.
/// </summary>
public class Household
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Équipement de la cuisine, partagée par les membres (plaques et micro-ondes par défaut :
    // le minimum le plus courant, notamment chez les étudiants).
    public List<KitchenEquipment> Equipment { get; set; } = [KitchenEquipment.Hob, KitchenEquipment.Microwave];

    public List<HouseholdMember> Members { get; set; } = [];

    public List<HouseholdInvitation> Invitations { get; set; } = [];
}
