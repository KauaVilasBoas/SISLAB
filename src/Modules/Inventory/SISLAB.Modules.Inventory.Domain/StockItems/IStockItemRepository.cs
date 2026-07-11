namespace SISLAB.Modules.Inventory.Domain.StockItems;

/// <summary>
/// Repository for the <see cref="StockItem"/> aggregate. The concrete implementation lives in the
/// module's Infrastructure project (EF Core, card [E3] #25). All lookups are implicitly tenant-scoped
/// by the write-side global query filter.
/// </summary>
public interface IStockItemRepository
{
    Task<StockItem?> FindByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(StockItem stockItem, CancellationToken ct = default);

    Task UpdateAsync(StockItem stockItem, CancellationToken ct = default);
}
