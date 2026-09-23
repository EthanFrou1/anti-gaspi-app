using Api.Dtos.Inventory;
using Api.Services.Inventory;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

// Données de référence, communes à tous : il suffit d'être connecté (FallbackPolicy).
[Route("api/categories")]
public sealed class CategoriesController(ICategoryService categoryService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> List(CancellationToken ct) =>
        Ok(await categoryService.ListAsync(ct));
}
