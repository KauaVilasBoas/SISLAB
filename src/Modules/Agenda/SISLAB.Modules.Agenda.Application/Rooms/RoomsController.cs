using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Agenda.Application.Rooms.Commands;
using SISLAB.Modules.Agenda.Application.Rooms.Queries;
using SISLAB.Modules.Agenda.Domain.Rooms;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Agenda.Application.Rooms;

[Route("api/rooms")]
[Authorize]
public sealed class RoomsController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public RoomsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<RoomListItem>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        IReadOnlyList<RoomListItem> rooms = await _mediator.SendAsync(new ListRoomsQuery(), ct);
        return Ok(new ApiResult<IReadOnlyList<RoomListItem>>(true, "Rooms retrieved.", rooms));
    }

    [HttpPost]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register([FromBody] RegisterRoomRequest body, CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(new RegisterRoomCommand(body.Name, body.Capacity, body.Type), ct);
        return Ok(new ApiResult<Guid>(true, "Room registered.", id));
    }

    [HttpGet("calendar")]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<BookingListItem>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCalendar([FromQuery] DateOnly date, CancellationToken ct)
    {
        IReadOnlyList<BookingListItem> bookings = await _mediator.SendAsync(new GetDailyCalendarQuery(date), ct);
        return Ok(new ApiResult<IReadOnlyList<BookingListItem>>(true, "Calendar retrieved.", bookings));
    }

    [HttpPost("bookings")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<CreateBookingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest body, CancellationToken ct)
    {
        CreateBookingResponse result = await _mediator.SendAsync(
            new CreateBookingCommand(body.RoomId, body.Activity, body.Date, body.StartTime, body.EndTime, body.Notes),
            ct);
        return Ok(new ApiResult<CreateBookingResponse>(true, "Booking created.", result));
    }

    [HttpDelete("bookings/{bookingId:guid}")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelBooking(Guid bookingId, CancellationToken ct)
    {
        await _mediator.SendAsync(new CancelBookingCommand(bookingId), ct);
        return Ok(new ApiResult(true, "Booking cancelled."));
    }
}

public sealed record RegisterRoomRequest(string Name, int Capacity, RoomType Type);

public sealed record CreateBookingRequest(
    Guid RoomId,
    AgendaActivity Activity,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? Notes);
