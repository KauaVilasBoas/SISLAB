using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.StockItems.Events;

/// <summary>
/// Raised when a stock entry increases the on-hand quantity of a <see cref="StockItem"/>. Carries the
/// received amount and the resulting balance so downstream consumers (read models, integrations) can
/// react without reloading the aggregate. <see cref="CompanyId"/> is carried so the Outbox translation
/// (card [E3] #26) can flatten it into the cross-tenant integration contract.
/// </summary>
/// <remarks>
/// <see cref="BatchId"/> identifies the <c>StockBatch</c> the entry created (card #109), so the ledger row can
/// point at the very batch and the cost report can attribute the receipt cost to it. <see cref="UnitCostBrl"/>
/// is the batch's unit price in BRL, or <see langword="null"/> for donations / no-invoice items.
/// <see cref="OccurredOn"/> and <see cref="SupplierPartnerId"/> are origin/traceability metadata supplied by
/// the operator (never inferred from the aggregate state). They travel on the event so the movements read
/// model (card [E4] #33) can record <c>when</c> the entry happened and <c>which supplier</c> it came from.
/// When the operator does not inform them, <see cref="OccurredOn"/> falls back to the emission instant and
/// <see cref="SupplierPartnerId"/> stays <see langword="null"/>. None of these is a domain invariant: the
/// aggregate carries them purely for the projection/audit trail.
/// </remarks>
public sealed record StockReceivedEvent(
    Guid CompanyId,
    Guid StockItemId,
    Guid BatchId,
    Quantity ReceivedQuantity,
    Quantity ResultingQuantity,
    Lot? Lot,
    ExpiryDate? Expiry,
    decimal? UnitCostBrl,
    DateOnly? OccurredOn = null,
    Guid? SupplierPartnerId = null) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
