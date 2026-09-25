using System.Text.Json;
using Api.Entities;
using Api.Services.Receipts;
using static Api.Tests.Receipts.ReceiptValidatorTests;

namespace Api.Tests.Receipts;

public class ReceiptPromptBuilderTests
{
    [Fact]
    public void Schema_RestrictsCategoriesAndUnits_ToClosedLists()
    {
        var schema = ReceiptPromptBuilder.OutputSchema(Categories);

        var line = schema["properties"].GetProperty("lines").GetProperty("items").GetProperty("properties");
        Assert.Equal(Categories.Select(c => c.Code), Strings(line.GetProperty("category").GetProperty("enum")));
        // Unités de l'app, plus les centilitres, acceptés en lecture seulement (convertis par le validateur).
        Assert.Equal([.. Enum.GetNames<QuantityUnit>(), "Centiliter"], Strings(line.GetProperty("unit").GetProperty("enum")));
    }

    [Fact]
    public void Schema_FollowsStructuredOutputRules()
    {
        // Sorties structurées : additionalProperties à false et tous les champs requis, à chaque niveau.
        var schema = ReceiptPromptBuilder.OutputSchema(Categories);
        var items = schema["properties"].GetProperty("lines").GetProperty("items");

        Assert.False(schema["additionalProperties"].GetBoolean());
        Assert.False(items.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            items.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order(),
            Strings(items.GetProperty("required")).Order());
    }

    [Fact]
    public void UserContent_GivesTodayAndTheCategories_AsJson()
    {
        var prompt = ReceiptPromptBuilder.Build(Categories, Today);

        Assert.Equal(ReceiptPromptBuilder.SystemPrompt, prompt.System);
        Assert.Contains("\"today\": \"2026-09-24\"", prompt.UserContent);
        // Accents non échappés : moins de tokens.
        Assert.Contains("\"name\": \"Viande hachée\"", prompt.UserContent);
    }

    // Les deux tests suivants ne vérifient pas le comportement du modèle (mesuré par
    // design/test-tickets/evaluate.py), mais qu'une règle apprise sur de vrais résultats ne
    // disparaît pas du prompt lors d'une réécriture.

    [Fact]
    public void QuantityRule_AsksToCopyThePrintedNumbers_WithoutCalculating()
    {
        var rule = Rule("6. Prix, exemplaires et contenu", "7. purchaseDate");

        // Le modèle recopie, l'API calcule le total (ReceiptValidator).
        Assert.Contains("recopie les nombres tels qu'ils sont imprimés, sans rien multiplier ni convertir", rule);
        // Multiplicateur sur la ligne de l'article ou sur une ligne voisine, dans les deux ordres.
        Assert.Contains("sur la ligne de l'article ou sur une ligne voisine", rule);
        Assert.Contains("« 3 x 1,20 »", rule);
        Assert.Contains("« 1,20 x 3 »", rule);
        // Le code TVA imprimé après le prix avait été lu comme une quantité.
        Assert.Contains("Un chiffre seul après le prix est un code de TVA, jamais une quantité", rule);
        // Prix unitaire : sert à vérifier le multiplicateur ; lot et centilitres lus tels quels.
        Assert.Contains("unitPrice : prix d'un exemplaire", rule);
        Assert.Contains("packSize : nombre d'unités du lot", rule);
        Assert.Contains("« 33CL » donne 33 Centiliter", rule);
        // L'exemple « 75CL » donné sans unité avait fait écrire « 75 Milliliter ».
        Assert.DoesNotContain("75CL", rule);
    }

    [Fact]
    public void Schema_FollowsTheReadingOrderOfTheReceipt()
    {
        var items = ReceiptPromptBuilder.OutputSchema(Categories)["properties"].GetProperty("lines").GetProperty("items");
        var properties = items.GetProperty("properties");

        // Le modèle écrit les champs dans l'ordre du schéma : prix, multiplicateur, puis contenu.
        Assert.Equal(
            ["receiptText", "isFood", "name", "category", "linePrice", "copies", "unitPrice", "packSize", "quantity", "unit"],
            properties.EnumerateObject().Select(p => p.Name));
        Assert.Equal("integer", properties.GetProperty("copies").GetProperty("type").GetString());
        Assert.Equal("integer", properties.GetProperty("packSize").GetProperty("type").GetString());
        // Prix illisible : null plutôt qu'un 0 inventé.
        Assert.Equal(["number", "null"], properties.GetProperty("unitPrice").GetProperty("anyOf").EnumerateArray()
            .Select(t => t.GetProperty("type").GetString()));
    }

    [Fact]
    public void ReceiptTextRule_KeepsOnlyTheArticleLabel()
    {
        var rule = Rule("2. receiptText", "3. isFood");

        Assert.Contains("sans le prix et sans la ligne de quantité ou de poids", rule);
    }
    /// <summary>Texte d'une règle du prompt système, espaces et retours à la ligne ramenés à un espace.</summary>
    private static string Rule(string start, string next)
    {
        var prompt = string.Join(' ', ReceiptPromptBuilder.SystemPrompt.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var from = prompt.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"Règle « {start} » introuvable dans le prompt.");
        var to = prompt.IndexOf(next, from, StringComparison.Ordinal);
        Assert.True(to > from, $"Règle « {next} » introuvable après « {start} ».");
        return prompt[from..to];
    }

    private static IEnumerable<string> Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!);
}
