using Api.Common;
using Api.Services.Ai;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Outils de mise au point : n'existent qu'en environnement Development (404 ailleurs).
/// </summary>
[Route("api/dev")]
public sealed class DevToolsController(AiUsageMeter meter) : ApiControllerBase
{
    /// <summary>
    /// Tokens consommés par les appels à Claude depuis le démarrage de l'API, par opération
    /// et par modèle (script d'évaluation de la lecture des tickets). Aucune donnée personnelle.
    /// </summary>
    [HttpGet("ai-usage")]
    [DevelopmentOnly]
    [ProducesResponseType<IReadOnlyList<AiUsage>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<AiUsage>> AiUsage() => Ok(meter.Snapshot());
}
