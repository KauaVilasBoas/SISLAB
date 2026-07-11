using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Infrastructure.Persistence;

namespace SISLAB.Modules.Inventory.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IStockItemRepository"/>. Reads are implicitly tenant-scoped by
/// the write-side global query filter; the actual commit is performed by the unit of work
/// (<c>TransactionBehavior</c> → <c>IUnitOfWork.SaveChangesAsync</c>), so the repository never saves.
/// </summary>
internal sealed class StockItemRepository : IStockItemRepository
{
    private readonly InventoryDbContext _dbContext;

    public StockItemRepository(InventoryDbContext dbContext) => _dbContext = dbContext;

    public async Task<StockItem?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.StockItems.FirstOrDefaultAsync(item => item.Id == id, ct);

    public async Task AddAsync(StockItem stockItem, CancellationToken ct = default)
        => await _dbContext.StockItems.AddAsync(stockItem, ct);

    public Task UpdateAsync(StockItem stockItem, CancellationToken ct = default)
    {
        // Tracked aggregates are already observed by the change tracker; Update is a no-op guard for
        // detached instances and keeps the intent explicit. SaveChanges is owned by the unit of work.
        _dbContext.StockItems.Update(stockItem);
        return Task.CompletedTask;
    }
}
