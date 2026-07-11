using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.StockItems.Events;

/// <summary>
/// Raised the moment a consumption drives a <see cref="StockItem"/>'s balance below its
/// <see cref="StockItem.MinimumQuantity"/> — i.e. when the balance <em>crosses</em> the threshold.
/// The aggregate emits it once per crossing (not on every consumption while already below), so the
/// low-stock alert (E6) fires exactly once. <see cref="CompanyId"/> is carried for the Outbox
/// translation (card [E3] #26).
/// </summary>
public sealed record StockBelowMinimumEvent(
    Guid CompanyId,
    Guid StockItemId,
    Quantity CurrentQuantity,
    Quantity MinimumQuantity) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
