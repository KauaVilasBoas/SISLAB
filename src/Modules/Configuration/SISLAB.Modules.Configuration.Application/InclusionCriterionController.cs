using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Configuration.Application.InclusionCriteria;
using SISLAB.Modules.Configuration.Domain.InclusionCriteria;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Application;

/// <summary>
/// HTTP boundary for the active company's animal-inclusion criteria (SISLAB-02): listing and creating the
/// configurable "(parameter, operator, threshold, unit)" selection rules (e.g. glicemia ≥ 250 mg/dL). The controller
/// only dispatches CQRS requests through <see cref="IMediator"/> and maps the result to the uniform
/// <see cref="ApiResult"/> envelope; it never touches repositories, the DbContext or Dapper.
/// </summary>
/// <remarks>
/// Tenant-scoped: every request runs against the active company resolved from the httpOnly cookie, never from the
/// request body. Each state-changing action is gated by Lumen's <c>[RequirePermission]</c>; the read is page-level
/// <c>[Authorize]</c>.
/// </remarks>
[Route("api/configuration/inclusion-criteria")]
[Authorize]
public sealed class InclusionCriterionController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public InclusionCriterionController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lists the active company's inclusion criteria, ordered by parameter.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<InclusionCriterionListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        IReadOnlyList<InclusionCriterionListItem> criteria =
            await _mediator.SendAsync(new ListInclusionCriteriaQuery(), ct);

        return Ok(new ApiResult<IReadOnlyList<InclusionCriterionListItem>>(true, "Inclusion criteria listed.", criteria));
    }

    /// <summary>Creates a new inclusion criterion for the active company.</summary>
    [HttpPost]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateInclusionCriterionRequest body, CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new CreateInclusionCriterionCommand(body.ParameterCode, body.Operator, body.Threshold, body.Unit),
            ct);

        return Ok(new ApiResult<Guid>(true, "Inclusion criterion created.", id));
    }
}

/// <summary>Request body for creating an inclusion criterion.</summary>
public sealed record CreateInclusionCriterionRequest(
    string ParameterCode,
    ComparisonOperator Operator,
    decimal Threshold,
    string Unit);
