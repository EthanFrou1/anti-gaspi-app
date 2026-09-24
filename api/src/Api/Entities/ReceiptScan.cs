namespace Api.Entities;

/// <summary>
/// Une lecture de ticket de caisse, conservée UNIQUEMENT pour les quotas : ni image, ni
/// produits lus, ni foyer (minimisation des données). Supprimée au bout de 48 heures.
/// </summary>
public class ReceiptScan
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // false pendant l'appel à l'IA (réservation d'une place dans les quotas).
    public bool IsCompleted { get; set; }
}
