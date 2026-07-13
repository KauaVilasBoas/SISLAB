using SISLAB.Modules.Inventory.Domain.StorageLocations;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements.Commands;

/// <summary>In-memory <see cref="IStorageLocationRepository"/> test double.</summary>
internal sealed class FakeStorageLocationRepository : IStorageLocationRepository
{
    private readonly Dictionary<Guid, StorageLocation> _locations = new();

    public FakeStorageLocationRepository Seed(StorageLocation location)
    {
        _locations[location.Id] = location;
        return this;
    }

    public Task<StorageLocation?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_locations.GetValueOrDefault(id));

    public Task AddAsync(StorageLocation storageLocation, CancellationToken ct = default)
    {
        _locations[storageLocation.Id] = storageLocation;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(StorageLocation storageLocation, CancellationToken ct = default)
    {
        _locations[storageLocation.Id] = storageLocation;
        return Task.CompletedTask;
    }
}
