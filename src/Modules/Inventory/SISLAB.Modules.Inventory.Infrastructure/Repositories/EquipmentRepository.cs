using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Inventory.Domain.Equipments;
using SISLAB.Modules.Inventory.Infrastructure.Persistence;

namespace SISLAB.Modules.Inventory.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IEquipmentRepository"/>. Reads are implicitly tenant-scoped by
/// the write-side global query filter; the owned calibration schedule and maintenance history load with
/// the aggregate. The commit is owned by the unit of work (<c>TransactionBehavior</c> →
/// <c>IUnitOfWork.SaveChangesAsync</c>), so the repository never saves.
/// </summary>
internal sealed class EquipmentRepository : IEquipmentRepository
{
    private readonly InventoryDbContext _dbContext;

    public EquipmentRepository(InventoryDbContext dbContext) => _dbContext = dbContext;

    public async Task<Equipment?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.Equipment.FirstOrDefaultAsync(equipment => equipment.Id == id, ct);

    public async Task AddAsync(Equipment equipment, CancellationToken ct = default)
        => await _dbContext.Equipment.AddAsync(equipment, ct);

    public Task UpdateAsync(Equipment equipment, CancellationToken ct = default)
    {
        _dbContext.Equipment.Update(equipment);
        return Task.CompletedTask;
    }
}
