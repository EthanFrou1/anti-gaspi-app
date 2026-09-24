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

    private static IEnumerable<string> Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!);
}
