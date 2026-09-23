using Api.Common;
using Api.Dtos.Recipes;
using Api.Services.Recipes;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/me/recipe-quota")]
public sealed class RecipeQuotaController(IRecipeService recipeService) : ApiControllerBase
{
    /// <summary>Recettes restantes aujourd'hui (remise à zéro à minuit, heure de Paris).</summary>
    [HttpGet]
    [ProducesResponseType<RecipeQuotaDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecipeQuotaDto>> Get(CancellationToken ct) =>
        Ok(await recipeService.GetQuotaAsync(User.GetUserId(), ct));
}
