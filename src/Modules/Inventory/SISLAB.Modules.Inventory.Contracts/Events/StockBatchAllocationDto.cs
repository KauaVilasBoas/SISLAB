namespace SISLAB.Modules.Inventory.Contracts.Events;

/// <summary>
/// Public, flattened slice of a stock draw-down (consumption or disposal) charged against a single batch:
/// how much was taken from the batch and the batch's unit cost at the time. Carried on the consumption and
/// disposal integration events so the movements read model can project one costed ledger row per slice
/// (card [E4] #109). This is the module's own DTO — it never leaks the internal <c>BatchAllocation</c> value
/// object.
/// </summary>
/// <remarks>
/// <see cref="UnitCostBrl"/> is <see langword="null"/> when the drawn batch has no recorded price (donation /
/// no-invoice), so the cost report treats that slice as unpriced without distorting the total.
/// </remarks>
public sealed record StockBatchAllocationDto(Guid BatchId, decimal Quantity, decimal? UnitCostBrl);
