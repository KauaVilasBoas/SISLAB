using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.StockItems.Events;

/// <summary>
/// Raised when a quantity of a <see cref="StockItem"/> is discarded (for example an expired or
/// unusable batch). Disposing expired stock is a legitimate operation and is never blocked; the reason
/// and audit trail for controlled items are handled downstream (cards [E3] #26 / #57).
/// </summary>
/// <remarks>
/// <see cref="CompanyId"/> is carried for the Outbox translation (card [E3] #26). <see cref="OccurredOn"/>
/// is origin/traceability metadata supplied by the operator: it travels on the event so the movements read
/// model (card [E7] #47) records <c>when</c> the disposal happened, falling back to the emission instant
/// when the operator does not inform it. Neither is a domain invariant.
/// </remarks>
public sealed record StockDisposedEvent(
    Guid CompanyId,
    Guid StockItemId,
    Quantity DisposedQuantity,
    Quantity ResultingQuantity,
    DateOnly? OccurredOn = null) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
