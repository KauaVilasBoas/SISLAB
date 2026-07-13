using SISLAB.Modules.Inventory.Domain.StockItems;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements.Commands;

/// <summary>
/// In-memory <see cref="IStockItemRepository"/> test double. Records whether the aggregate was handed
/// back for persistence so handler tests can assert the save-side wiring without a database.
/// </summary>
internal sealed class FakeStockItemRepository : IStockItemRepository
{
    private readonly Dictionary<Guid, StockItem> _items = new();

    public StockItem? LastUpdated { get; private set; }

    public FakeStockItemRepository Seed(StockItem item)
    {
        _items[item.Id] = item;
        return this;
    }

    public Task<StockItem?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_items.GetValueOrDefault(id));

    public Task AddAsync(StockItem stockItem, CancellationToken ct = default)
    {
        _items[stockItem.Id] = stockItem;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(StockItem stockItem, CancellationToken ct = default)
    {
        _items[stockItem.Id] = stockItem;
        LastUpdated = stockItem;
        return Task.CompletedTask;
    }
}
