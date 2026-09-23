using Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Convertit une erreur métier en réponse HTTP au format standard ProblemDetails (RFC 9457).
    /// Le champ « code » permet à l'app mobile de réagir sans analyser le message.
    /// </summary>
    protected ActionResult ToProblem(Error error)
    {
        if (error.Type == ErrorType.Validation && error.ValidationErrors is not null)
        {
            foreach (var (field, messages) in error.ValidationErrors)
            {
                foreach (var message in messages)
                {
                    ModelState.AddModelError(field, message);
                }
            }

            var validation = ProblemDetailsFactory.CreateValidationProblemDetails(
                HttpContext, ModelState, StatusCodes.Status400BadRequest);
            // Affecté après la création : le titre générique défini dans Program.cs
            // ne doit pas écraser le message propre à l'erreur métier.
            validation.Title = error.Message;
            validation.Extensions["code"] = error.Code;
            return BadRequest(validation);
        }

        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError,
        };

        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, status, title: error.Message);
        problem.Extensions["code"] = error.Code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}
