namespace SISLAB.Modules.Inventory.Domain.StorageLocations;

/// <summary>
/// Repository for the <see cref="StorageLocation"/> aggregate. The concrete implementation lives in the
/// module's Infrastructure project (EF Core, card [E3] #25). All lookups are implicitly tenant-scoped
/// by the write-side global query filter.
/// </summary>
public interface IStorageLocationRepository
{
    Task<StorageLocation?> FindByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(StorageLocation storageLocation, CancellationToken ct = default);

    Task UpdateAsync(StorageLocation storageLocation, CancellationToken ct = default);
}
