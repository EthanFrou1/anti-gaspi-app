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
        Assert.Equal(Enum.GetNames<QuantityUnit>(), Strings(line.GetProperty("unit").GetProperty("enum")));
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
    public void QuantityRule_MultipliesTheContentOfOneCopy_ByTheNumberOfCopies()
    {
        var rule = Rule("6. quantity et unit", "7. purchaseDate");

        // Multiplicateur sur la ligne de l'article ou sur une ligne voisine, dans les deux ordres.
        Assert.Contains("sur sa ligne ou sur une ligne voisine", rule);
        Assert.Contains("« 3 x 1,20 »", rule);
        Assert.Contains("« 1,20 x 3 »", rule);
        Assert.Contains("quantity = nombre d'exemplaires × contenu d'un exemplaire", rule);
        // Lot acheté plusieurs fois : le multiplicateur s'ajoute au lot, il ne le remplace pas.
        Assert.Contains("2 lots « X6 » donnent 12 Piece", rule);
        Assert.Contains("Un multiplicateur s'applique toujours, même si le libellé indique déjà un lot", rule);
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
