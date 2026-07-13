using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Configuration.Application.ReferenceRanges;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Application;

/// <summary>
/// HTTP boundary for the active company's reference ranges (card [E12] #76): listing and creating the healthy
/// intervals of analytes, scoped by species/strain. The controller only dispatches CQRS requests through
/// <see cref="IMediator"/> and maps the result to the uniform <see cref="ApiResult"/> envelope; it never
/// touches repositories, the DbContext or Dapper.
/// </summary>
/// <remarks>
/// Tenant-scoped: every request runs against the active company resolved from the httpOnly cookie, never from
/// the request body. RBAC permissions are layered on in card #77.
/// </remarks>
[Route("api/configuration/reference-ranges")]
[Authorize]
public sealed class ReferenceRangeController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public ReferenceRangeController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lists the active company's reference ranges, ordered by analyte then species.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<ReferenceRangeListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        IReadOnlyList<ReferenceRangeListItem> ranges =
            await _mediator.SendAsync(new ListReferenceRangesQuery(), ct);

        return Ok(new ApiResult<IReadOnlyList<ReferenceRangeListItem>>(true, "Reference ranges listed.", ranges));
    }

    /// <summary>Creates a new reference range for the active company.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateReferenceRangeRequest body, CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new CreateReferenceRangeCommand(
                body.Analyte, body.Species, body.Minimum, body.Maximum, body.Unit),
            ct);

        return Ok(new ApiResult<Guid>(true, "Reference range created.", id));
    }
}

/// <summary>Request body for creating a reference range.</summary>
public sealed record CreateReferenceRangeRequest(
    string Analyte,
    string Species,
    decimal? Minimum,
    decimal? Maximum,
    string? Unit);
