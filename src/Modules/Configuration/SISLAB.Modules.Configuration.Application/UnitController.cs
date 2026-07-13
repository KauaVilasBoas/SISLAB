using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Configuration.Application.Units;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Application;

/// <summary>
/// HTTP boundary for the active company's units of measure/consumption (card [E12] #76): listing and
/// creating them. The controller only dispatches CQRS requests through <see cref="IMediator"/> and maps the
/// result to the uniform <see cref="ApiResult"/> envelope; it never touches repositories, the DbContext or
/// Dapper.
/// </summary>
/// <remarks>
/// Tenant-scoped: every request runs against the active company resolved from the httpOnly cookie, never from
/// the request body. RBAC permissions are layered on in card #77.
/// </remarks>
[Route("api/configuration/units")]
[Authorize]
public sealed class UnitController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public UnitController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lists the active company's units, ordered by symbol.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<UnitListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        IReadOnlyList<UnitListItem> units = await _mediator.SendAsync(new ListUnitsQuery(), ct);

        return Ok(new ApiResult<IReadOnlyList<UnitListItem>>(true, "Units listed.", units));
    }

    /// <summary>Creates a new unit for the active company.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateUnitRequest body, CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(new CreateUnitCommand(body.Symbol, body.Name), ct);

        return Ok(new ApiResult<Guid>(true, "Unit created.", id));
    }
}

/// <summary>Request body for creating a unit.</summary>
public sealed record CreateUnitRequest(string Symbol, string Name);
