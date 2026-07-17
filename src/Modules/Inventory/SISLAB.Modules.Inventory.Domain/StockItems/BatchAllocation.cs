using SISLAB.Modules.Inventory.Domain.ValueObjects;

namespace SISLAB.Modules.Inventory.Domain.StockItems;

/// <summary>
/// One slice of a stock draw-down (consumption or disposal) charged against a single <see cref="StockBatch"/>:
/// how much was taken from that batch and the batch's unit cost at the time. A single operation may produce
/// several allocations when it spills across batches under FEFO, each carrying the batch it drew from and its
/// own cost — this is what lets the cost report (card #109) value the movement at the real per-batch price
/// rather than a blurred average.
/// </summary>
/// <remarks>
/// Emitted on the stock movement domain events so the read model can project one costed ledger row per
/// allocation. <see cref="UnitCostBrl"/> is <see langword="null"/> when the drawn batch has no recorded price
/// (donation / no-invoice), so the report can treat that slice as unpriced without distorting the total.
/// </remarks>
public sealed record BatchAllocation(Guid BatchId, Quantity Quantity, decimal? UnitCostBrl);
