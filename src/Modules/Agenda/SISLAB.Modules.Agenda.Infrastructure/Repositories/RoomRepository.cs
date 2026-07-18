using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Agenda.Domain.Rooms;
using SISLAB.Modules.Agenda.Infrastructure.Persistence;

namespace SISLAB.Modules.Agenda.Infrastructure.Repositories;

internal sealed class RoomRepository : IRoomRepository
{
    private readonly AgendaDbContext _db;

    public RoomRepository(AgendaDbContext db) => _db = db;

    public Task<Room?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Rooms.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Room>> GetActiveAsync(CancellationToken cancellationToken = default)
        => await _db.Rooms.Where(r => r.IsActive).OrderBy(r => r.Name).ToListAsync(cancellationToken);

    public void Add(Room room) => _db.Rooms.Add(room);

    public void Remove(Room room) => _db.Rooms.Remove(room);
}

internal sealed class BookingRepository : IBookingRepository
{
    private readonly AgendaDbContext _db;

    public BookingRepository(AgendaDbContext db) => _db = db;

    public Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Bookings.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetByRoomAndDateAsync(
        Guid roomId,
        DateOnly date,
        CancellationToken cancellationToken = default)
        => await _db.Bookings
            .Where(b => b.RoomId == roomId && b.Date == date && b.Status == BookingStatus.Active)
            .ToListAsync(cancellationToken);

    public void Add(Booking booking) => _db.Bookings.Add(booking);
}
