using Api.Entities;

namespace Api.Data.Seed;

/// <summary>
/// Données de référence des catégories, insérées par migration (HasData).
///
/// Durées : conservation typique au réfrigérateur (ou au placard pour l'épicerie)
/// à partir de l'achat, produit non entamé. Ce sont des estimations prudentes,
/// que l'utilisateur corrige avec la date imprimée sur l'emballage.
///
/// Attention : modifier ces valeurs impose une nouvelle migration, et un Id existant
/// ne doit jamais changer une fois publié (les produits et l'app s'y réfèrent).
/// </summary>
public static class CategorySeed
{
    public const int PriorityDefault = 0;
    public const int PriorityStorage = 10;

    public static readonly ProductCategory[] Categories =
    [
        Category(1, "ground-meat", "Viande hachée", 1, ExpiryKind.UseBy),
        Category(2, "fresh-meat", "Viande fraîche", 3, ExpiryKind.UseBy),
        Category(3, "poultry", "Volaille", 2, ExpiryKind.UseBy),
        Category(4, "fish-seafood", "Poisson, fruits de mer", 2, ExpiryKind.UseBy),
        Category(5, "cold-cuts", "Charcuterie", 7, ExpiryKind.UseBy),
        Category(6, "eggs", "Œufs", 21, ExpiryKind.BestBefore),
        Category(7, "fresh-milk", "Lait frais", 7, ExpiryKind.UseBy),
        Category(8, "uht-milk", "Lait UHT", 90, ExpiryKind.BestBefore),
        Category(9, "yogurts", "Yaourts, desserts lactés", 21, ExpiryKind.UseBy),
        Category(10, "fresh-cheese", "Fromage frais", 7, ExpiryKind.UseBy),
        // Camembert, brie, reblochon, bleus, chèvre… : croûte fleurie ou lavée, DLC.
        Category(11, "soft-cheese", "Fromage à pâte molle", 14, ExpiryKind.UseBy),
        // Comté, emmental, parmesan, cantal, raclette… : pâte pressée, se garde plusieurs semaines.
        Category(12, "hard-cheese", "Fromage à pâte dure", 30, ExpiryKind.BestBefore),
        Category(13, "butter", "Beurre", 60, ExpiryKind.BestBefore),
        Category(14, "cream", "Crème", 14, ExpiryKind.UseBy),
        Category(15, "fruits", "Fruits", 7, ExpiryKind.BestBefore),
        Category(16, "vegetables", "Légumes", 7, ExpiryKind.BestBefore),
        Category(17, "leafy-greens", "Salade, herbes", 4, ExpiryKind.BestBefore),
        Category(18, "bread", "Pain", 3, ExpiryKind.BestBefore),
        Category(19, "ready-meals", "Traiteur, plats préparés", 3, ExpiryKind.UseBy),
        Category(20, "leftovers", "Restes faits maison", 3, ExpiryKind.UseBy),
        Category(21, "frozen", "Surgelés", 180, ExpiryKind.BestBefore),
        Category(22, "canned", "Conserves", 730, ExpiryKind.BestBefore),
        Category(23, "dry-goods", "Épicerie sèche", 365, ExpiryKind.BestBefore),
        Category(24, "condiments", "Sauces, condiments", 90, ExpiryKind.BestBefore),
        Category(25, "drinks", "Boissons", 30, ExpiryKind.BestBefore),
        Category(26, "other", "Autre", 7, ExpiryKind.BestBefore),
    ];

    // Étiquettes vérifiées dans la taxonomie Open Food Facts (API /api/v2/taxonomy).
    // Les catégories sont désignées par leur code : une faute de frappe fait échouer
    // le démarrage (et les tests) au lieu de pointer silencieusement vers la mauvaise catégorie.
    public static readonly OffCategoryMapping[] OffMappings =
    [
        Map("en:ground-meats", "ground-meat"),
        Map("en:meats", "fresh-meat"), Map("en:beef", "fresh-meat"), Map("en:beef-steaks", "fresh-meat"),
        Map("en:pork", "fresh-meat"), Map("en:veal-meat", "fresh-meat"), Map("en:lamb-meat", "fresh-meat"),
        Map("en:poultries", "poultry"), Map("en:chickens", "poultry"), Map("en:chicken-breasts", "poultry"),
        Map("en:turkeys", "poultry"),
        Map("en:fishes", "fish-seafood"), Map("en:seafood", "fish-seafood"), Map("en:crustaceans", "fish-seafood"),
        Map("en:smoked-salmons", "fish-seafood"),
        Map("en:prepared-meats", "cold-cuts"), Map("en:hams", "cold-cuts"), Map("en:sausages", "cold-cuts"),
        Map("en:dry-sausages", "cold-cuts"), Map("en:bacon", "cold-cuts"),
        Map("en:eggs", "eggs"), Map("en:chicken-eggs", "eggs"),
        Map("en:milks", "fresh-milk"), Map("en:fresh-milks", "fresh-milk"),
        Map("en:uht-milks", "uht-milk", PriorityStorage),
        Map("en:yogurts", "yogurts"), Map("en:dairy-desserts", "yogurts"), Map("en:fermented-milk-products", "yogurts"),
        Map("en:fresh-cheeses", "fresh-cheese"), Map("en:cream-cheeses", "fresh-cheese"), Map("en:mozzarella", "fresh-cheese"),

        // Fromage sans précision : pâte molle, l'hypothèse la plus prudente (DLC, durée courte).
        Map("en:cheeses", "soft-cheese"),
        Map("en:soft-cheeses", "soft-cheese"), Map("en:camemberts", "soft-cheese"), Map("en:bries", "soft-cheese"),
        Map("en:reblochon", "soft-cheese"), Map("en:munster", "soft-cheese"), Map("en:saint-nectaire", "soft-cheese"),
        Map("en:blue-veined-cheeses", "soft-cheese"), Map("en:roquefort-cheeses", "soft-cheese"),
        Map("en:goat-cheeses", "soft-cheese"),
        Map("en:hard-cheeses", "hard-cheese"), Map("en:uncooked-pressed-cheeses", "hard-cheese"),
        Map("en:comte", "hard-cheese"), Map("en:emmentaler", "hard-cheese"), Map("en:parmigiano-reggiano", "hard-cheese"),
        Map("en:grana-padano", "hard-cheese"), Map("en:beaufort", "hard-cheese"), Map("en:cantal", "hard-cheese"),
        Map("en:morbier", "hard-cheese"), Map("en:raclette", "hard-cheese"), Map("en:gouda", "hard-cheese"),
        Map("en:cheddar-cheese", "hard-cheese"), Map("en:grated-cheese", "hard-cheese"),

        Map("en:butters", "butter"), Map("en:salted-butters", "butter"),
        Map("en:creams", "cream"), Map("en:sour-creams", "cream"),
        Map("en:fruits", "fruits"), Map("en:fresh-fruits", "fruits"),
        Map("en:vegetables", "vegetables"), Map("en:fresh-vegetables", "vegetables"),
        Map("en:leaf-vegetables", "leafy-greens"), Map("en:lettuces", "leafy-greens"), Map("en:aromatic-plants", "leafy-greens"),
        Map("en:breads", "bread"), Map("en:sliced-breads", "bread"), Map("en:viennoiseries", "bread"),
        Map("en:meals", "ready-meals"), Map("en:fresh-meals", "ready-meals"), Map("en:pizzas", "ready-meals"),
        Map("en:sandwiches", "ready-meals"),
        Map("en:frozen-foods", "frozen", PriorityStorage), Map("en:frozen-vegetables", "frozen", PriorityStorage),
        Map("en:ice-creams", "frozen", PriorityStorage),
        Map("en:canned-foods", "canned", PriorityStorage), Map("en:canned-vegetables", "canned", PriorityStorage),
        Map("en:canned-fishes", "canned", PriorityStorage),
        Map("en:dried-products", "dry-goods", PriorityStorage),
        Map("en:pastas", "dry-goods"), Map("en:rices", "dry-goods"), Map("en:cereals-and-their-products", "dry-goods"),
        Map("en:flours", "dry-goods"), Map("en:legumes", "dry-goods"), Map("en:pulses", "dry-goods"),
        Map("en:breakfast-cereals", "dry-goods"), Map("en:biscuits", "dry-goods"), Map("en:snacks", "dry-goods"),
        Map("en:chocolates", "dry-goods"),
        Map("en:condiments", "condiments"), Map("en:sauces", "condiments"), Map("en:mustards", "condiments"),
        Map("en:ketchup", "condiments"), Map("en:salad-dressings", "condiments"), Map("en:jams", "condiments"),
        Map("en:honeys", "condiments"), Map("en:spreads", "condiments"), Map("en:vegetable-oils", "condiments"),
        Map("en:beverages", "drinks"), Map("en:waters", "drinks"), Map("en:sodas", "drinks"), Map("en:fruit-juices", "drinks"),
    ];

    private static ProductCategory Category(int id, string code, string name, int shelfLifeDays, ExpiryKind kind) =>
        new() { Id = id, Code = code, Name = name, DefaultShelfLifeDays = shelfLifeDays, ExpiryKind = kind };

    private static OffCategoryMapping Map(string offTag, string categoryCode, int priority = PriorityDefault) =>
        new()
        {
            OffTag = offTag,
            // Single : lève une exception si le code n'existe pas (ou existe en double).
            CategoryId = Categories.Single(c => c.Code == categoryCode).Id,
            Priority = priority,
        };
}
