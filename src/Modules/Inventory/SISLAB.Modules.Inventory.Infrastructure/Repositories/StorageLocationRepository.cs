using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.Modules.Inventory.Infrastructure.Persistence;

namespace SISLAB.Modules.Inventory.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IStorageLocationRepository"/>. Reads are implicitly
/// tenant-scoped by the write-side global query filter; the commit is owned by the unit of work
/// (<c>TransactionBehavior</c> → <c>IUnitOfWork.SaveChangesAsync</c>), so the repository never saves.
/// </summary>
internal sealed class StorageLocationRepository : IStorageLocationRepository
{
    private readonly InventoryDbContext _dbContext;

    public StorageLocationRepository(InventoryDbContext dbContext) => _dbContext = dbContext;

    public async Task<StorageLocation?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.StorageLocations.FirstOrDefaultAsync(location => location.Id == id, ct);

    public async Task AddAsync(StorageLocation storageLocation, CancellationToken ct = default)
        => await _dbContext.StorageLocations.AddAsync(storageLocation, ct);

    public Task UpdateAsync(StorageLocation storageLocation, CancellationToken ct = default)
    {
        _dbContext.StorageLocations.Update(storageLocation);
        return Task.CompletedTask;
    }
}
