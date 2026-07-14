using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Configuration.Domain.Rooms;
using SISLAB.Modules.Configuration.Infrastructure.Persistence;

namespace SISLAB.Modules.Configuration.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRoomRepository"/>. Reads are implicitly tenant-scoped by the
/// write-side global query filter. The commit is owned by the unit of work.
/// </summary>
internal sealed class RoomRepository : IRoomRepository
{
    private readonly ConfigurationDbContext _dbContext;

    public RoomRepository(ConfigurationDbContext dbContext) => _dbContext = dbContext;

    public async Task<Room?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.Rooms.FirstOrDefaultAsync(room => room.Id == id, ct);

    public async Task AddAsync(Room room, CancellationToken ct = default)
        => await _dbContext.Rooms.AddAsync(room, ct);

    public Task UpdateAsync(Room room, CancellationToken ct = default)
    {
        _dbContext.Rooms.Update(room);
        return Task.CompletedTask;
    }
}
