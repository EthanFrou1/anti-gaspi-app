using System.ComponentModel.DataAnnotations;
using Api.Entities;

namespace Api.Dtos.Households;

public sealed record UpdateEquipmentRequest(
    [Required] IReadOnlyList<KitchenEquipment> Equipment);
