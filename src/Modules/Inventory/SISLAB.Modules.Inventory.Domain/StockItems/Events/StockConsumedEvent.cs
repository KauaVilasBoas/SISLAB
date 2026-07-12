using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.StockItems.Events;

/// <summary>
/// Raised when a consumption reduces the on-hand quantity of a <see cref="StockItem"/>. Crossing of the
/// minimum threshold is signalled separately by <see cref="StockBelowMinimumEvent"/>, so consumers do
/// not infer it from the resulting quantity. <see cref="CompanyId"/> is carried for the Outbox
/// translation (card [E3] #26).
/// </summary>
/// <remarks>
/// <see cref="OccurredOn"/> and <see cref="ExperimentId"/> are origin/traceability metadata supplied by
/// the operator. They travel on the event so the movements read model (card [E4] #33) and the consumption
/// report (card #31) can record <c>when</c> the consumption happened and <c>which experiment</c> it fed.
/// <see cref="ExperimentId"/> is a cross-module reference held <b>by value</b> (Guid), with no FK or
/// navigation to the Experiment module. Neither field is a domain invariant; <see cref="OccurredOn"/>
/// falls back to the emission instant when the operator does not inform it.
/// </remarks>
public sealed record StockConsumedEvent(
    Guid CompanyId,
    Guid StockItemId,
    Quantity ConsumedQuantity,
    Quantity ResultingQuantity,
    DateOnly? OccurredOn = null,
    Guid? ExperimentId = null) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
