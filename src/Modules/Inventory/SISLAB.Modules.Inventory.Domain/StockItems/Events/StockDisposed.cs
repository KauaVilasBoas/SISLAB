using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.StockItems.Events;

/// <summary>
/// Raised when a quantity of a <see cref="StockItem"/> is discarded (for example an expired or
/// unusable batch). Disposing expired stock is a legitimate operation and is never blocked; the reason
/// and audit trail for controlled items are handled downstream (cards [E3] #26 / #57).
/// </summary>
public sealed record StockDisposed(
    Guid StockItemId,
    Quantity DisposedQuantity,
    Quantity ResultingQuantity) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
