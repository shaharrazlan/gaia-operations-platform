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
public class OperationsController(OperationService operations, CatalogService catalog) : ControllerBase
{
    /// <summary>Adds a custom formula operation. It can be run right away, no restart needed (R4.AC1).</summary>
    /// <response code="201">The new operation, including its "version".</response>
    /// <response code="400">validation_error / formula_invalid — "field" says which input is wrong.</response>
    /// <response code="409">duplicate_id — the id is taken (also by a deleted operation).</response>
    [HttpPost]
    [ProducesResponseType<OperationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OperationDto>> Create(OperationUpsertDto request, CancellationToken cancellationToken)
    {
        var created = OperationDto.From(await catalog.CreateAsync(request.ToInput(), cancellationToken));
        return Created($"/api/operations/{created.Id}", created);
    }

    /// <summary>
    /// Changes operation {id}. Send back the "version" you loaded: if someone else saved in between,
    /// the change is rejected with 409 edit_conflict instead of overwriting theirs (R4.AC7).
    /// Built-in operations can be renamed, relabelled and disabled, but their logic can't change (R4.AC6).
    /// </summary>
    /// <response code="200">The saved operation with its new "version".</response>
    /// <response code="400">validation_error / formula_invalid / system_logic_readonly.</response>
    /// <response code="404">operation_not_found.</response>
    /// <response code="409">edit_conflict — reload the operation and apply your change again.</response>
    [HttpPut("{id}")]
    [ProducesResponseType<OperationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<OperationDto> Update(string id, OperationUpsertDto request, CancellationToken cancellationToken) =>
        OperationDto.From(await catalog.UpdateAsync(id, request.ToInput(), cancellationToken));

    /// <summary>
    /// Lists the operations users can run (enabled, not deleted), ordered by name.
    /// With includeDisabled=true it is the management list: disabled operations are included too.
    /// </summary>
    [HttpGet]
    public async Task<IEnumerable<OperationDto>> List(bool includeDisabled, CancellationToken cancellationToken) =>
        (includeDisabled
            ? await catalog.ListAllAsync(cancellationToken)
            : await operations.ListAsync(cancellationToken))
        .Select(OperationDto.From);

    /// <summary>
    /// Removes custom operation {id} from the catalog. Its row and history are kept; the id stays taken (R4.AC3).
    /// Built-in operations can't be deleted — disable them instead.
    /// </summary>
    /// <response code="204">Removed.</response>
    /// <response code="400">system_cannot_delete.</response>
    /// <response code="404">operation_not_found.</response>
    /// <response code="409">edit_conflict — someone changed it at the same moment; reload and try again.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await catalog.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Runs operation {id} on Field A and Field B and returns the result.</summary>
    /// <response code="400">validation_error — a field is missing or not valid for this operation.</response>
    /// <response code="404">operation_not_found — unknown, disabled or deleted operation.</response>
    /// <response code="422">domain_error — the inputs are valid but there is no result (e.g. division by zero).</response>
    /// <response code="502">external_service_error / 504 external_service_timeout — e.g. the weather service failed.</response>
    [HttpPost("{id}/execute")]
    [ProducesResponseType<ExecuteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ExecuteResponse> Execute(string id, ExecuteRequest request, CancellationToken cancellationToken)
    {
        var result = await operations.ExecuteAsync(id, request.FieldA, request.FieldB, cancellationToken);
        return new ExecuteResponse(result.OperationId, result.FieldA, result.FieldB, result.Result);
    }
}
