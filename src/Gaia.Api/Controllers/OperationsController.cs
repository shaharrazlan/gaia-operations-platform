using Gaia.Api.Contracts;
using Gaia.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gaia.Api.Controllers;

/// <summary>
/// The platform's public API. Thin on purpose: it translates HTTP to service calls and back;
/// every rule lives in Gaia.Core. Errors are turned into ProblemDetails by the exception handler.
/// </summary>
[ApiController]
[Route("api/operations")]
[Produces("application/json")]
public class OperationsController(OperationService operations) : ControllerBase
{
    /// <summary>Lists the operations users can run (enabled, not deleted), ordered by name.</summary>
    [HttpGet]
    public async Task<IEnumerable<OperationDto>> List(CancellationToken cancellationToken) =>
        (await operations.ListAsync(cancellationToken)).Select(OperationDto.From);
}
