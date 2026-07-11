using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.StockItems.Events;

/// <summary>
/// Raised when a physical stock count (conference) is recorded for a <see cref="StockItem"/>, typically
/// the periodic inventory of controlled substances. The count is an append-only compliance record: it
/// captures the counted balance and the divergence against the system balance <b>without changing the
/// on-hand quantity</b> (decision recorded on card [E3] #24). Any correction follows the normal entry or
/// disposal flow. The append-only audit trail that consumes this event is owned by card #57.
/// </summary>
/// <param name="StockItemId">Item whose balance was counted.</param>
/// <param name="SystemQuantity">Balance the system held at the moment of the count.</param>
/// <param name="CountedQuantity">Physical balance reported by the operator.</param>
/// <param name="Divergence">
/// Counted minus system balance: positive when the physical count is higher than the system, negative
/// when lower, zero when they match.
/// </param>
public sealed record StockCountedEvent(
    Guid StockItemId,
    Quantity SystemQuantity,
    Quantity CountedQuantity,
    decimal Divergence) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
