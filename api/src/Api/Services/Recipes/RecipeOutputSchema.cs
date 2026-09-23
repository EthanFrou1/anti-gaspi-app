using System.Text.Json;

namespace Api.Services.Recipes;

/// <summary>
/// Format de sortie IMPOSÉ à l'IA (structured outputs) : la réponse est garantie conforme
/// à ce schéma. Les sorties structurées ne gèrent ni minimum/maximum ni maxLength : ces bornes
/// sont vérifiées ensuite par RecipeValidator.
/// </summary>
public static class RecipeOutputSchema
{
    public const string Json =
        """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["title", "prepMinutes", "servings", "ingredients", "steps"],
          "properties": {
            "title": { "type": "string" },
            "prepMinutes": { "type": "integer" },
            "servings": { "type": "integer" },
            "ingredients": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["name", "quantity", "inventoryRef"],
                "properties": {
                  "name": { "type": "string" },
                  "quantity": { "type": "string" },
                  "inventoryRef": { "anyOf": [{ "type": "string" }, { "type": "null" }] }
                }
              }
            },
            "steps": { "type": "array", "items": { "type": "string" } }
          }
        }
        """;

    // Format attendu par le SDK : dictionnaire des propriétés de premier niveau du schéma.
    public static readonly IReadOnlyDictionary<string, JsonElement> Schema =
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Json)!;
}
