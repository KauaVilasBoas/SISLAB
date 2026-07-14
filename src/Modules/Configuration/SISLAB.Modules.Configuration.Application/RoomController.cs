using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Configuration.Application.Rooms;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Application;

/// <summary>
/// HTTP boundary for the active company's rooms (card [E12] #76): listing and creating them, including the
/// "requires authorization" flag the future Agenda module will consume. The controller only dispatches CQRS
/// requests through <see cref="IMediator"/> and maps the result to the uniform <see cref="ApiResult"/>
/// envelope; it never touches repositories, the DbContext or Dapper.
/// </summary>
/// <remarks>
/// Tenant-scoped: every request runs against the active company resolved from the httpOnly cookie, never from
/// the request body. RBAC permissions are layered on in card #77.
/// </remarks>
[Route("api/configuration/rooms")]
[Authorize]
public sealed class RoomController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public RoomController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lists the active company's rooms, ordered by name.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<RoomListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        IReadOnlyList<RoomListItem> rooms = await _mediator.SendAsync(new ListRoomsQuery(), ct);

        return Ok(new ApiResult<IReadOnlyList<RoomListItem>>(true, "Rooms listed.", rooms));
    }

    /// <summary>Creates a new room for the active company.</summary>
    [HttpPost]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateRoomRequest body, CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new CreateRoomCommand(body.Name, body.RequiresAuthorization), ct);

        return Ok(new ApiResult<Guid>(true, "Room created.", id));
    }
}

/// <summary>Request body for creating a room.</summary>
public sealed record CreateRoomRequest(string Name, bool RequiresAuthorization);
