using SISLAB.Modules.Inventory.Contracts.Events;
using SISLAB.Modules.Inventory.Domain.StockItems.Events;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Infrastructure.Messaging;

/// <summary>
/// Translates the internal <see cref="StockTransferredEvent"/> into the public
/// <see cref="StockTransferredIntegrationEvent"/> before it is written to the Outbox. The moved quantity
/// (the whole balance) is flattened to a primitive with its unit symbol for the movements ledger.
/// </summary>
internal sealed class StockTransferredEventTranslator
    : IDomainEventToIntegrationEventTranslator<StockTransferredEvent>
{
    public IIntegrationEvent Translate(StockTransferredEvent domainEvent) =>
        new StockTransferredIntegrationEvent(
            eventId: Guid.NewGuid(),
            occurredOnUtc: domainEvent.OccurredOnUtc,
            companyId: domainEvent.CompanyId,
            stockItemId: domainEvent.StockItemId,
            fromStorageLocationId: domainEvent.FromStorageLocationId,
            toStorageLocationId: domainEvent.ToStorageLocationId,
            movedQuantity: domainEvent.MovedQuantity.Value,
            unit: domainEvent.MovedQuantity.Unit.Symbol,
            occurredOn: domainEvent.OccurredOn);
}
