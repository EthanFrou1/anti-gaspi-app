using Api.Common;
using Api.Dtos.Users;
using Api.Services.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

// Pas d'attribut [Authorize] : tous les endpoints exigent un utilisateur connecté
// par défaut (FallbackPolicy dans Program.cs).
[Route("api/me")]
public sealed class MeController(IUserService userService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Get(CancellationToken ct)
    {
        var result = await userService.GetAsync(User.GetUserId(), ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }
}
