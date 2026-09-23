using Api.Common;
using Api.Dtos.Profiles;
using Api.Services.Profiles;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

// Le profil de l'utilisateur connecté uniquement : aucun identifiant dans l'URL,
// donc impossible de lire ou modifier le profil de quelqu'un d'autre.
[Route("api/me/profile")]
public sealed class ProfileController(IProfileService profileService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileDto>> Get(CancellationToken ct)
    {
        var result = await profileService.GetAsync(User.GetUserId(), ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }

    [HttpPut]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProfileDto>> Save(SaveProfileRequest request, CancellationToken ct)
    {
        var result = await profileService.SaveAsync(User.GetUserId(), request, ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }
}
