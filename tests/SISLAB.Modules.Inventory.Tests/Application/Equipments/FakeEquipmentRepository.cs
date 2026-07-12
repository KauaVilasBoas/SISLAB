using SISLAB.Modules.Inventory.Domain.Equipments;

namespace SISLAB.Modules.Inventory.Tests.Application.Equipments;

/// <summary>
/// In-memory <see cref="IEquipmentRepository"/> test double. Records the last aggregate handed back for
/// persistence so handler tests can assert the save-side wiring without a database.
/// </summary>
internal sealed class FakeEquipmentRepository : IEquipmentRepository
{
    private readonly Dictionary<Guid, Equipment> _equipment = new();

    public Equipment? LastAdded { get; private set; }

    public Equipment? LastUpdated { get; private set; }

    public FakeEquipmentRepository Seed(Equipment equipment)
    {
        _equipment[equipment.Id] = equipment;
        return this;
    }

    public Task<Equipment?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_equipment.GetValueOrDefault(id));

    public Task AddAsync(Equipment equipment, CancellationToken ct = default)
    {
        _equipment[equipment.Id] = equipment;
        LastAdded = equipment;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Equipment equipment, CancellationToken ct = default)
    {
        _equipment[equipment.Id] = equipment;
        LastUpdated = equipment;
        return Task.CompletedTask;
    }
}
