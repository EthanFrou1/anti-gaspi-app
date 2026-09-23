namespace Api.Entities;

/// <summary>
/// Correspondance entre une catégorie Open Food Facts (ex. « en:yogurts ») et une
/// catégorie de l'app. Stockée en base pour pouvoir l'enrichir sans redéployer.
/// </summary>
public class OffCategoryMapping
{
    // Étiquette OFF, en minuscules avec le préfixe de langue (ex. « en:yogurts »).
    public required string OffTag { get; set; }

    public int CategoryId { get; set; }
    public ProductCategory Category { get; set; } = null!;

    // Départage quand un produit a plusieurs étiquettes connues. Les modes de
    // conservation (surgelé, conserve, UHT…) l'emportent sur la nature du produit :
    // des haricots verts en conserve se gardent comme une conserve, pas comme un légume frais.
    public int Priority { get; set; }
}
