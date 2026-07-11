using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.StockItems.Events;

/// <summary>
/// Raised when a stock entry increases the on-hand quantity of a <see cref="StockItem"/>. Carries the
/// received amount and the resulting balance so downstream consumers (read models, integrations) can
/// react without reloading the aggregate. <see cref="CompanyId"/> is carried so the Outbox translation
/// (card [E3] #26) can flatten it into the cross-tenant integration contract.
/// </summary>
public sealed record StockReceivedEvent(
    Guid CompanyId,
    Guid StockItemId,
    Quantity ReceivedQuantity,
    Quantity ResultingQuantity,
    Lot? Lot,
    ExpiryDate? Expiry) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
