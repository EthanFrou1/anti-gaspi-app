using Api.Authorization;
using Api.Common;
using Api.Dtos.Recipes;
using Api.Services.Recipes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

// Réservé aux membres du foyer présent dans l'URL.
[Route("api/households/{householdId:guid}/recipes")]
[Authorize(Policy = HouseholdPolicies.Member)]
public sealed class RecipesController(IRecipeService recipeService) : ApiControllerBase
{
    /// <summary>
    /// Génère une recette avec les produits du foyer (quota : 3 par jour et par utilisateur).
    /// </summary>
    [HttpPost]
    [ProducesResponseType<RecipeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RecipeDto>> Generate(Guid householdId, GenerateRecipeRequest request, CancellationToken ct)
    {
        var result = await recipeService.GenerateAsync(User.GetUserId(), householdId, request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { householdId, recipeId = result.Value.Id }, result.Value)
            : ToProblem(result.Error);
    }

    /// <summary>
    /// Mise au point du prompt : renvoie le prompt exact sans appeler l'IA ni décompter le quota.
    /// N'existe qu'en environnement Development.
    /// </summary>
    [HttpPost("prompt-preview")]
    [DevelopmentOnly]
    [ProducesResponseType<RecipePromptPreviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecipePromptPreviewDto>> PreviewPrompt(
        Guid householdId, GenerateRecipeRequest request, CancellationToken ct)
    {
        var result = await recipeService.PreviewPromptAsync(User.GetUserId(), householdId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }

    /// <summary>Historique du foyer (30 derniers jours).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RecipeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RecipeDto>>> List(Guid householdId, CancellationToken ct) =>
        Ok(await recipeService.ListAsync(householdId, ct));

    [HttpGet("{recipeId:guid}")]
    [ProducesResponseType<RecipeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecipeDto>> Get(Guid householdId, Guid recipeId, CancellationToken ct)
    {
        var result = await recipeService.GetAsync(householdId, recipeId, ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }

    /// <summary>
    /// « J'ai cuisiné cette recette » : marque comme consommés les produits cochés.
    /// </summary>
    [HttpPost("{recipeId:guid}/cooked")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> MarkCooked(Guid householdId, Guid recipeId, MarkRecipeCookedRequest request, CancellationToken ct)
    {
        var result = await recipeService.MarkCookedAsync(User.GetUserId(), householdId, recipeId, request.FinishedItemIds, ct);
        return result.IsSuccess ? Ok(new { consumedCount = result.Value }) : ToProblem(result.Error);
    }
}
