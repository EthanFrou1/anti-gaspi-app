using System.Globalization;
using System.Text;
using Api.Entities;
using Api.Services.Ai;
using Api.Services.Profiles;

namespace Api.Services.Recipes;

/// <summary>Nature d'une règle : un goût se nomme dans les journaux, une contrainte jamais.</summary>
public enum PreferenceKind
{
    // Aliments « Pas pour moi », « Pas épicé ».
    Taste,
    // Allergènes (profils ou invités) et exclusions : jamais nommés dans les journaux.
    Constraint,
}

/// <summary>Règle que le texte enfreint.</summary>
public sealed record PreferenceHit(string Label, PreferenceKind Kind);

/// <summary>
/// Recette rejetée parce qu'elle enfreint un goût ou une contrainte d'un convive : 503, quota
/// non décompté (comme toute réponse invalide), mais comptée à part dans les journaux pour
/// savoir si c'est fréquent. Un goût y est nommé ; une contrainte (allergène, exclusion), jamais.
/// </summary>
public sealed class RecipeRejectedByPreferencesException(PreferenceHit hit)
    : AiUnavailableException(hit.Kind == PreferenceKind.Taste
        ? $"Réponse de l'IA invalide : aliment écarté par les préférences (« {hit.Label} »)."
        : "Réponse de l'IA invalide : contrainte du repas non respectée.")
{
    public PreferenceHit Hit { get; } = hit;

    /// <summary>Ce qui peut figurer dans les journaux : le goût, ou seulement « contrainte du repas ».</summary>
    public string LoggableReason => Hit.Kind == PreferenceKind.Taste ? Hit.Label : "contrainte du repas";
}

/// <summary>
/// Goûts et contraintes des convives, vérifiés par mots-clés. Fonction pure. Sert deux fois :
/// avant l'IA, pour écarter les produits du frigo concernés ; après, pour rejeter une recette
/// qui en contiendrait. Pour les allergènes, c'est un filet de sécurité, pas une garantie
/// (un bouillon cube ne dit pas qu'il contient du gluten) : l'app avertit toujours.
/// </summary>
/// <remarks>
/// Recherche par mots entiers, sans accents ni majuscules, au singulier comme au pluriel
/// (« tomates », « choux ») : « ail » ne trouve ni « caille » ni « travail ». Mots courts
/// volontairement ABSENTS, trop ambigus : « bar », « lieu », « sole » (poissons), « bleu »,
/// « pois », « chou », « raisin » (seuls « petits pois », « chou-fleur », « raisins secs »
/// comptent). Une mention négative (« sans oignon », « ni ail », « pas d'ail ») est ignorée,
/// tout comme un mot suivi d'une mention qui l'annule (« pâtes sans gluten »).
/// </remarks>
public static class FoodPreferences
{
    /// <param name="SafeSuffixes">Mentions qui, juste APRÈS le mot-clé, l'annulent (« sans gluten »).</param>
    public sealed record Rule(
        string Label,
        IReadOnlyList<string> Keywords,
        IReadOnlyList<string> Exceptions,
        IReadOnlyList<string>? SafeSuffixes = null);

    public static IReadOnlyDictionary<DislikedFood, Rule> Dislikes { get; } = new Dictionary<DislikedFood, Rule>
    {
        [DislikedFood.Mushrooms] = new("champignons", ["champignon", "cèpe", "girolle", "chanterelle", "pleurote", "shiitake", "morille"], []),
        [DislikedFood.Onion] = new("oignon", ["oignon"], []),
        [DislikedFood.Garlic] = new("ail", ["ail", "aulx", "aïoli", "pesto", "tzatziki"], []),
        [DislikedFood.Leek] = new("poireau", ["poireau"], []),
        [DislikedFood.BellPepper] = new("poivron", ["poivron"], []),
        [DislikedFood.Eggplant] = new("aubergine", ["aubergine"], []),
        [DislikedFood.Zucchini] = new("courgette", ["courgette"], []),
        [DislikedFood.Cucumber] = new("concombre", ["concombre", "tzatziki"], []),
        [DislikedFood.Broccoli] = new("brocoli", ["brocoli"], []),
        [DislikedFood.Cauliflower] = new("chou-fleur", ["chou-fleur"], []),
        [DislikedFood.BrusselsSprouts] = new("choux de Bruxelles", ["chou de Bruxelles"], []),
        [DislikedFood.Spinach] = new("épinards", ["épinard"], []),
        [DislikedFood.Beetroot] = new("betterave", ["betterave"], []),
        [DislikedFood.Celery] = new("céleri", ["céleri"], []),
        [DislikedFood.Fennel] = new("fenouil", ["fenouil"], []),
        [DislikedFood.Endive] = new("endive", ["endive", "chicon"], []),
        [DislikedFood.Radish] = new("radis", ["radis"], []),
        [DislikedFood.Turnip] = new("navet", ["navet"], []),
        // « petits pois » seulement : « pois chiche » et « pois cassés » sont des légumineuses.
        [DislikedFood.Peas] = new("petits pois", ["petit pois"], []),
        [DislikedFood.Tomato] = new("tomate", ["tomate", "ketchup", "passata"], []),
        [DislikedFood.Avocado] = new("avocat", ["avocat", "guacamole"], []),
        [DislikedFood.Olives] = new("olives", ["olive", "tapenade"], ["huile d'olive", "huile olive"]),
        [DislikedFood.Pickles] = new("cornichons", ["cornichon"], []),
        [DislikedFood.Coriander] = new("coriandre", ["coriandre"], []),
        [DislikedFood.Fish] = new("poisson", [
            "poisson", "saumon", "thon", "cabillaud", "colin", "merlu", "merlan", "sardine", "maquereau", "truite",
            "anchois", "hareng", "dorade", "daurade", "lotte", "églefin", "haddock", "tilapia", "panga",
            "surimi", "nuoc-mâm",
        ], []),
        [DislikedFood.Lamb] = new("agneau", ["agneau", "mouton", "gigot", "merguez"], []),
        [DislikedFood.BlueCheese] = new("fromage bleu", [
            "fromage bleu", "roquefort", "bleu d'Auvergne", "bleu de Bresse", "fourme d'Ambert", "gorgonzola", "stilton",
        ], []),
        [DislikedFood.GoatCheese] = new("fromage de chèvre", ["chèvre", "crottin", "rocamadour", "picodon", "chabichou"], []),
        [DislikedFood.Legumes] = new("légumineuses", [
            "lentille", "pois chiche", "pois cassé", "haricot rouge", "haricot blanc", "haricot coco", "flageolet", "fève", "houmous",
        ], []),
        [DislikedFood.Tofu] = new("tofu", ["tofu"], []),
        // « Coco » seul désigne la noix de coco, sauf pour le haricot coco (une légumineuse).
        [DislikedFood.Coconut] = new("noix de coco", ["coco"], ["haricot coco", "coco de Paimpol"]),
        [DislikedFood.Raisins] = new("raisins secs", ["raisin sec"], []),
    };

    public static Rule Spicy { get; } = new("épicé", [
        "piment", "harissa", "tabasco", "sriracha", "sambal", "wasabi", "cayenne", "jalapeño", "chipotle",
        "piquant", "piquante", "curry fort", "chili",
    ], ["piment doux"]);

    /// <summary>
    /// Allergènes vérifiés par mots-clés, qu'ils viennent d'un profil ou des invités d'un repas.
    /// Les autres allergènes restent confiés à l'IA (et au filtre par catégorie du frigo).
    /// </summary>
    public static IReadOnlyDictionary<Allergen, Rule> Allergens { get; } = new Dictionary<Allergen, Rule>
    {
        // « Noix de » désigne parfois tout autre chose qu'un fruit à coque.
        [Allergen.TreeNuts] = new("fruits à coque", [
            "noix", "noisette", "amande", "cajou", "pistache", "pécan", "macadamia", "pignon", "praliné", "frangipane",
            "pesto", "nougat", "gianduja", "massepain",
        ], ["noix de coco", "noix de muscade", "noix de Saint-Jacques", "noix de veau", "noix de beurre", "beurre noisette", "pomme noisette"]),
        [Allergen.Peanuts] = new("arachides", ["arachide", "cacahuète", "cacahouète", "satay", "peanut"], []),
        [Allergen.Gluten] = new("gluten", [
            "blé", "farine", "pâte", "pain", "chapelure", "pané", "panée", "semoule", "boulgour", "couscous", "orge", "seigle",
            "épeautre", "avoine", "malt", "seitan", "biscuit", "biscotte", "brioche", "croissant", "croûton", "bière",
            "sauce soja", "nouille", "vermicelle", "tortilla", "wrap", "béchamel",
            "spaghetti", "tagliatelle", "penne", "macaroni", "coquillette", "fusilli", "farfalle", "lasagne", "ravioli",
            "tortellini", "gnocchi",
        ], [
            "pâte d'amande", "pâte de curry", "pâte de riz", "farine de riz", "farine de maïs", "farine de sarrasin",
            "farine de pois chiche", "farine de châtaigne", "farine de coco", "semoule de maïs", "semoule de riz",
            "nouille de riz", "vermicelle de riz", "tortilla de maïs",
        ], ["sans gluten"]),
        // Aucun produit laitier : « lait sans lactose » reste du lait (libellé côté app).
        [Allergen.Milk] = new("produits laitiers", [
            "lait", "crème", "beurre", "ghee", "fromage", "yaourt", "yogourt", "mozzarella", "parmesan", "ricotta",
            "mascarpone", "emmental", "gruyère", "comté", "cheddar", "feta", "chèvre", "béchamel", "lactose",
        ], [
            "lait de coco", "lait d'amande", "lait d'avoine", "lait de soja", "lait de riz", "lait de noisette",
            "crème de coco", "crème de soja", "crème d'avoine", "crème de riz", "crème de marrons",
            "beurre de cacahuète", "beurre de cacahouète", "beurre d'amande", "beurre de cajou",
        ]),
    };

    /// <summary>Exclusions vérifiées par mots-clés (les autres restent confiées à l'IA).</summary>
    public static IReadOnlyDictionary<IngredientExclusion, Rule> Exclusions { get; } = new Dictionary<IngredientExclusion, Rule>
    {
        [IngredientExclusion.Pork] = new("porc", [
            "porc", "jambon", "lardon", "bacon", "chorizo", "saucisson", "saucisse", "chipolata", "rillettes", "saindoux",
            "pancetta", "coppa", "prosciutto", "speck", "mortadelle", "andouille", "andouillette", "boudin", "cervelas",
            "rosette", "gélatine",
        ], [
            "jambon de dinde", "jambon de poulet", "jambon de volaille",
            "lardon de volaille", "lardon de dinde", "lardon de poulet",
            "bacon de dinde", "bacon de poulet",
            "saucisse de volaille", "saucisse de dinde", "saucisse de poulet",
            "rillettes de thon", "rillettes de saumon", "rillettes de sardine", "rillettes de poulet",
            "gélatine de poisson", "gélatine de bœuf",
        ]),
    };

    // Mots qui rendent la mention négative quand ils la précèdent (« sans oignon »).
    private static readonly HashSet<string> Negations = ["sans", "ni", "pas"];

    // Petits mots permis entre la négation et l'aliment (« sans les champignons », « pas d'ail »).
    private static readonly HashSet<string> Articles = ["d", "de", "du", "des", "l", "la", "le", "les"];

    private sealed record Compiled(string[][] Keywords, string[][] Exceptions, string[][] SafeSuffixes);

    // Mots-clés découpés une fois pour toutes (listes fermées, fixes).
    private static readonly Dictionary<Rule, Compiled> Tokenized =
        Dislikes.Values.Append(Spicy).Concat(Allergens.Values).Concat(Exclusions.Values).ToDictionary(
            rule => rule,
            rule => new Compiled(
                rule.Keywords.Select(Tokenize).ToArray(),
                rule.Exceptions.Select(Tokenize).ToArray(),
                (rule.SafeSuffixes ?? []).Select(Tokenize).ToArray()));

    /// <summary>Première règle des convives que le texte enfreint, ou null.</summary>
    public static PreferenceHit? FindIn(string? text, MealConstraints constraints) =>
        FindIn(text, constraints.Dislikes, constraints.AvoidSpicy, constraints.Allergens, constraints.Exclusions);

    public static PreferenceHit? FindIn(
        string? text,
        IReadOnlyCollection<DislikedFood> dislikes,
        bool avoidSpicy,
        IReadOnlyCollection<Allergen>? allergens = null,
        IReadOnlyCollection<IngredientExclusion>? exclusions = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var tastes = dislikes.Select(d => Dislikes[d]).Concat(avoidSpicy ? [Spicy] : Array.Empty<Rule>());
        var constraints = (allergens ?? []).Where(Allergens.ContainsKey).Select(a => Allergens[a])
            .Concat((exclusions ?? []).Where(Exclusions.ContainsKey).Select(e => Exclusions[e]));

        var rules = tastes.Select(rule => (rule, PreferenceKind.Taste))
            .Concat(constraints.Select(rule => (rule, PreferenceKind.Constraint)))
            .ToList();
        if (rules.Count == 0)
        {
            return null;
        }

        var tokens = Tokenize(text);
        foreach (var (rule, kind) in rules)
        {
            if (Matches(tokens, Tokenized[rule]))
            {
                return new PreferenceHit(rule.Label, kind);
            }
        }
        return null;
    }

    private static bool Matches(string[] tokens, Compiled rule)
    {
        // Les expressions permises (« huile d'olive ») masquent leurs mots avant la recherche.
        var masked = new bool[tokens.Length];
        foreach (var exception in rule.Exceptions)
        {
            foreach (var start in Occurrences(tokens, exception))
            {
                Array.Fill(masked, true, start, exception.Length);
            }
        }

        return rule.Keywords.Any(keyword => Occurrences(tokens, keyword).Any(start =>
            !masked.AsSpan(start, keyword.Length).Contains(true)
            && !IsNegated(tokens, start)
            && !IsFollowedBy(tokens, start + keyword.Length, rule.SafeSuffixes)));
    }

    private static IEnumerable<int> Occurrences(string[] tokens, string[] phrase)
    {
        for (var start = 0; start + phrase.Length <= tokens.Length; start++)
        {
            if (StartsAt(tokens, start, phrase))
            {
                yield return start;
            }
        }
    }

    private static bool StartsAt(string[] tokens, int start, string[] phrase)
    {
        if (start + phrase.Length > tokens.Length)
        {
            return false;
        }
        for (var i = 0; i < phrase.Length; i++)
        {
            if (!SameWord(tokens[start + i], phrase[i]))
            {
                return false;
            }
        }
        return true;
    }

    // Singulier ou pluriel en -s / -x (« tomates », « choux », « poireaux »).
    private static bool SameWord(string token, string keyword) =>
        token == keyword || token == keyword + "s" || token == keyword + "x";

    private static bool IsNegated(string[] tokens, int start)
    {
        var before = start - 1;
        if (before >= 0 && Articles.Contains(tokens[before]))
        {
            before--;
        }
        return before >= 0 && Negations.Contains(tokens[before]);
    }

    // « pâtes sans gluten » : la mention qui annule vient juste après le mot-clé.
    private static bool IsFollowedBy(string[] tokens, int end, string[][] suffixes) =>
        suffixes.Any(suffix => StartsAt(tokens, end, suffix));

    /// <summary>
    /// Minuscules, sans accents (« Cèpes » → « cepes »), découpé en mots : tout ce qui n'est
    /// ni lettre ni chiffre sépare (espaces, apostrophes, tirets : « chou-fleur », « d'ail »).
    /// </summary>
    private static string[] Tokenize(string text)
    {
        var decomposed = text.ToLowerInvariant().Replace("œ", "oe").Replace("æ", "ae").Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }
        return builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}
