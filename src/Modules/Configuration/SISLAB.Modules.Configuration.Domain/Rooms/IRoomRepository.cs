namespace SISLAB.Modules.Configuration.Domain.Rooms;

/// <summary>
/// Repository for <see cref="Room"/> aggregates (interface in the Domain, implementation in the
/// Infrastructure). Reads are implicitly tenant-scoped by the write-side global query filter.
/// </summary>
public interface IRoomRepository
{
    /// <summary>Returns the room with <paramref name="id"/> for the active company, or <see langword="null"/>.</summary>
    Task<Room?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Adds a new room for the active company.</summary>
    Task AddAsync(Room room, CancellationToken ct = default);

    /// <summary>Marks an existing room as modified so the unit of work persists the change.</summary>
    Task UpdateAsync(Room room, CancellationToken ct = default);
}
