namespace Api.Entities;

/// <summary>
/// Catégorie de produit (table de référence). Porte la durée de conservation par
/// défaut qui sert à estimer la date de péremption à l'ajout d'un produit.
/// </summary>
public class ProductCategory
{
    // Identifiants fixes (données de référence insérées par migration) :
    // ils ne doivent jamais changer, l'app mobile et les produits s'y réfèrent.
    public int Id { get; set; }

    // Identifiant lisible et stable (ex. « ground-meat »), utile pour le code et les icônes.
    public required string Code { get; set; }

    public required string Name { get; set; }

    // Durée typique après l'achat, produit non entamé, conservé correctement.
    public int DefaultShelfLifeDays { get; set; }

    public ExpiryKind ExpiryKind { get; set; }
}
