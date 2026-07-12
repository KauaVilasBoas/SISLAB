namespace SISLAB.Modules.Inventory.Domain.Equipments;

/// <summary>
/// Repository for the <see cref="Equipment"/> aggregate. The concrete implementation lives in the
/// module's Infrastructure project (EF Core). All lookups are implicitly tenant-scoped by the write-side
/// global query filter; the maintenance history loads with the aggregate.
/// </summary>
public interface IEquipmentRepository
{
    Task<Equipment?> FindByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Equipment equipment, CancellationToken ct = default);

    Task UpdateAsync(Equipment equipment, CancellationToken ct = default);
}
