using SISLAB.Modules.Inventory.Contracts.Events;
using SISLAB.Modules.Inventory.Domain.StockItems;

namespace SISLAB.Modules.Inventory.Infrastructure.Messaging;

/// <summary>
/// Flattens the internal <see cref="BatchAllocation"/> value objects into the public
/// <see cref="StockBatchAllocationDto"/> contracts before a movement event is written to the Outbox, so
/// consumers never depend on the Inventory domain (card [E3] #26).
/// </summary>
internal static class BatchAllocationMapper
{
    public static IReadOnlyList<StockBatchAllocationDto> ToDtos(IReadOnlyList<BatchAllocation> allocations)
        => allocations
            .Select(allocation => new StockBatchAllocationDto(
                allocation.BatchId,
                allocation.Quantity.Value,
                allocation.UnitCostBrl))
            .ToList();
}
