using SISLAB.Modules.Inventory.Contracts.Events;
using SISLAB.Modules.Inventory.Domain.StockItems.Events;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Infrastructure.Messaging;

/// <summary>
/// Translates the internal <see cref="StockReceivedEvent"/> into the public
/// <see cref="StockReceivedIntegrationEvent"/> before it is written to the Outbox. Flattens the
/// <c>Quantity</c>/<c>Lot</c>/<c>ExpiryDate</c> value objects into primitives so consumers never
/// depend on the Inventory domain.
/// </summary>
internal sealed class StockReceivedEventTranslator
    : IDomainEventToIntegrationEventTranslator<StockReceivedEvent>
{
    public IIntegrationEvent Translate(StockReceivedEvent domainEvent) =>
        new StockReceivedIntegrationEvent(
            eventId: Guid.NewGuid(),
            occurredOnUtc: domainEvent.OccurredOnUtc,
            companyId: domainEvent.CompanyId,
            stockItemId: domainEvent.StockItemId,
            stockBatchId: domainEvent.BatchId,
            receivedQuantity: domainEvent.ReceivedQuantity.Value,
            resultingQuantity: domainEvent.ResultingQuantity.Value,
            unit: domainEvent.ResultingQuantity.Unit.Symbol,
            lotCode: domainEvent.Lot?.Code,
            expiryYear: domainEvent.Expiry?.Year,
            expiryMonth: domainEvent.Expiry?.Month,
            unitCostBrl: domainEvent.UnitCostBrl,
            occurredOn: domainEvent.OccurredOn,
            supplierPartnerId: domainEvent.SupplierPartnerId);
}
