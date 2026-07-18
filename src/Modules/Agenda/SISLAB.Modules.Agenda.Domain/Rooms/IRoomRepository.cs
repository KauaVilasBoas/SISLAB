namespace SISLAB.Modules.Agenda.Domain.Rooms;

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Room>> GetActiveAsync(CancellationToken cancellationToken = default);
    void Add(Room room);
    void Remove(Room room);
}

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns all active bookings in the given room on the given date (for overlap detection).</summary>
    Task<IReadOnlyList<Booking>> GetByRoomAndDateAsync(
        Guid roomId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    void Add(Booking booking);
}
