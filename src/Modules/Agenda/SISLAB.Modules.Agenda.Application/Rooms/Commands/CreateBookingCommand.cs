using SISLAB.Modules.Agenda.Domain.Rooms;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Agenda.Application.Rooms.Commands;

public sealed record CreateBookingCommand(
    Guid RoomId,
    AgendaActivity Activity,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? Notes) : ICommand<CreateBookingResponse>;

public sealed record CreateBookingResponse(Guid BookingId, bool ConflictWarning);

internal sealed class CreateBookingCommandHandler : ICommandHandler<CreateBookingCommand, CreateBookingResponse>
{
    private readonly IRoomRepository _rooms;
    private readonly IBookingRepository _bookings;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditActorAccessor _actorAccessor;
    private readonly IClock _clock;

    public CreateBookingCommandHandler(
        IRoomRepository rooms,
        IBookingRepository bookings,
        ITenantContext tenantContext,
        IAuditActorAccessor actorAccessor,
        IClock clock)
    {
        _rooms = rooms;
        _bookings = bookings;
        _tenantContext = tenantContext;
        _actorAccessor = actorAccessor;
        _clock = clock;
    }

    public async Task<CreateBookingResponse> HandleAsync(
        CreateBookingCommand command,
        CancellationToken cancellationToken = default)
    {
        Room? room = await _rooms.GetByIdAsync(command.RoomId, cancellationToken);
        if (room is null || !room.IsActive)
            throw new NotFoundException($"Room {command.RoomId} not found or inactive.");

        // Detect overlaps — alert, do NOT block (project decision card #69).
        IReadOnlyList<Booking> existingOnDay = await _bookings.GetByRoomAndDateAsync(
            command.RoomId,
            command.Date,
            cancellationToken);

        bool hasConflict = existingOnDay.Any(b =>
            b.OverlapsWith(command.Date, command.StartTime, command.EndTime));

        string bookedByName = _actorAccessor.GetCurrentActor();

        Booking booking = Booking.Create(
            _tenantContext.CompanyId,
            command.RoomId,
            bookedByName,
            command.Activity,
            command.Date,
            command.StartTime,
            command.EndTime,
            command.Notes,
            overlapsExist: hasConflict,
            _clock.UtcNow);

        _bookings.Add(booking);
        return new CreateBookingResponse(booking.Id, hasConflict);
    }
}
