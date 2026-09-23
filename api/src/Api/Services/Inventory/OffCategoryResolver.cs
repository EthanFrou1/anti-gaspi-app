namespace Api.Services.Inventory;

/// <summary>
/// Règle de correspondance d'une étiquette Open Food Facts vers une catégorie de l'app.
/// </summary>
public sealed record OffTagRule(int CategoryId, int Priority);

/// <summary>
/// Choisit la catégorie de l'app la plus adaptée à partir des étiquettes OFF d'un produit.
/// Fonction pure : les règles viennent de la base, la décision est testable sans elle.
/// </summary>
public static class OffCategoryResolver
{
    /// <summary>
    /// OFF liste les catégories du plus général au plus précis
    /// (ex. « en:dairies », « en:fermented-milk-products », « en:yogurts »).
    /// On retient :
    /// 1. la règle de plus haute priorité (mode de conservation : surgelé, conserve…) ;
    /// 2. à priorité égale, l'étiquette la plus précise, c'est-à-dire la dernière de la liste.
    /// Renvoie null si aucune étiquette n'est connue : l'utilisateur choisira lui-même.
    /// </summary>
    public static int? Resolve(IReadOnlyList<string> offTags, IReadOnlyDictionary<string, OffTagRule> rules)
    {
        OffTagRule? best = null;
        var bestPosition = -1;

        for (var position = 0; position < offTags.Count; position++)
        {
            if (!rules.TryGetValue(offTags[position].Trim().ToLowerInvariant(), out var rule))
            {
                continue;
            }

            var isBetter = best is null
                || rule.Priority > best.Priority
                || (rule.Priority == best.Priority && position > bestPosition);

            if (isBetter)
            {
                best = rule;
                bestPosition = position;
            }
        }

        return best?.CategoryId;
    }
}
