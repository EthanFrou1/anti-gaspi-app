namespace Api.Entities;

/// <summary>
/// Type de date de péremption (réglementation française).
/// </summary>
public enum ExpiryKind
{
    // DLC — « à consommer jusqu'au » : ne pas dépasser (risque sanitaire).
    UseBy = 0,

    // DDM — « à consommer de préférence avant » : encore consommable après la date,
    // seules les qualités gustatives peuvent baisser. Ne jamais la présenter comme « périmée ».
    BestBefore = 1,
}
