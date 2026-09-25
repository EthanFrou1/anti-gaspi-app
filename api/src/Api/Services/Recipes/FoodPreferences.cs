using System.Globalization;
using System.Text;
using Api.Entities;
using Api.Services.Ai;
using Api.Services.Profiles;

namespace Api.Services.Recipes;

/// <summary>
/// Recette rejetée parce qu'elle contient un aliment écarté par les goûts d'un convive :
/// 503, quota non décompté (comme toute réponse invalide), mais comptée à part dans les
/// journaux pour savoir si c'est fréquent. Seul l'aliment est connu, jamais le convive.
/// </summary>
public sealed class RecipeRejectedByPreferencesException(string preference)
    : AiUnavailableException($"Réponse de l'IA invalide : aliment écarté par les préférences (« {preference} »).")
{
    public string Preference { get; } = preference;
}

/// <summary>
/// Goûts des convives (« Pas pour moi », « Pas épicé ») : mots-clés de chaque aliment et
/// recherche dans un texte. Fonction pure. Sert deux fois : avant l'IA, pour écarter les
/// produits du frigo concernés ; après, pour rejeter une recette qui en contiendrait.
/// </summary>
/// <remarks>
/// Recherche par mots entiers, sans accents ni majuscules, au singulier comme au pluriel
/// (« tomates », « choux ») : « ail » ne trouve ni « caille » ni « travail ». Mots courts
/// volontairement ABSENTS, trop ambigus : « bar », « lieu », « sole » (poissons), « bleu »,
/// « pois », « chou », « raisin » (seuls « petits pois », « chou-fleur », « raisins secs »
/// comptent). Une mention négative (« sans oignon », « ni ail », « pas d'ail ») est ignorée.
/// </remarks>
public static class FoodPreferences
{
    public sealed record Rule(string Label, IReadOnlyList<string> Keywords, IReadOnlyList<string> Exceptions);

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

    // Mots qui rendent la mention négative quand ils la précèdent (« sans oignon »).
    private static readonly HashSet<string> Negations = ["sans", "ni", "pas"];

    // Petits mots permis entre la négation et l'aliment (« sans les champignons », « pas d'ail »).
    private static readonly HashSet<string> Articles = ["d", "de", "du", "des", "l", "la", "le", "les"];

    // Mots-clés découpés une fois pour toutes (listes fermées, fixes).
    private static readonly Dictionary<Rule, (string[][] Keywords, string[][] Exceptions)> Tokenized =
        Dislikes.Values.Append(Spicy).ToDictionary(
            rule => rule,
            rule => (rule.Keywords.Select(Tokenize).ToArray(), rule.Exceptions.Select(Tokenize).ToArray()));

    /// <summary>Libellé de la première préférence que le texte enfreint, ou null.</summary>
    public static string? FindIn(string? text, MealConstraints constraints) =>
        FindIn(text, constraints.Dislikes, constraints.AvoidSpicy);

    public static string? FindIn(string? text, IReadOnlyCollection<DislikedFood> dislikes, bool avoidSpicy)
    {
        if (string.IsNullOrWhiteSpace(text) || (dislikes.Count == 0 && !avoidSpicy))
        {
            return null;
        }

        var tokens = Tokenize(text);
        var rules = dislikes.Select(d => Dislikes[d]).Concat(avoidSpicy ? [Spicy] : Array.Empty<Rule>());
        return rules.FirstOrDefault(rule => Matches(tokens, Tokenized[rule]))?.Label;
    }

    private static bool Matches(string[] tokens, (string[][] Keywords, string[][] Exceptions) rule)
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

        return rule.Keywords.Any(keyword => Occurrences(tokens, keyword)
            .Any(start => !masked.AsSpan(start, keyword.Length).Contains(true) && !IsNegated(tokens, start)));
    }

    private static IEnumerable<int> Occurrences(string[] tokens, string[] phrase)
    {
        for (var start = 0; start + phrase.Length <= tokens.Length; start++)
        {
            var found = true;
            for (var i = 0; i < phrase.Length && found; i++)
            {
                found = SameWord(tokens[start + i], phrase[i]);
            }
            if (found)
            {
                yield return start;
            }
        }
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
