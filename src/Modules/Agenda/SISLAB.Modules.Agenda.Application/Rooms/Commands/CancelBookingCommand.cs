using SISLAB.Modules.Agenda.Domain.Rooms;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Agenda.Application.Rooms.Commands;

public sealed record CancelBookingCommand(Guid BookingId) : ICommand;

internal sealed class CancelBookingCommandHandler : ICommandHandler<CancelBookingCommand>
{
    private readonly IBookingRepository _bookings;

    public CancelBookingCommandHandler(IBookingRepository bookings) => _bookings = bookings;

    public async Task<Unit> HandleAsync(CancelBookingCommand command, CancellationToken cancellationToken = default)
    {
        Booking? booking = await _bookings.GetByIdAsync(command.BookingId, cancellationToken);
        if (booking is null)
            throw new NotFoundException($"Booking {command.BookingId} not found.");

        booking.Cancel();
        return Unit.Value;
    }
}
