using SISLAB.Modules.Inventory.Contracts.Events;
using SISLAB.Modules.Inventory.Domain.StockItems.Events;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Infrastructure.Messaging;

/// <summary>
/// Translates the internal <see cref="StockConsumedEvent"/> into the public
/// <see cref="StockConsumedIntegrationEvent"/> before it is written to the Outbox.
/// </summary>
internal sealed class StockConsumedEventTranslator
    : IDomainEventToIntegrationEventTranslator<StockConsumedEvent>
{
    public IIntegrationEvent Translate(StockConsumedEvent domainEvent) =>
        new StockConsumedIntegrationEvent(
            eventId: Guid.NewGuid(),
            occurredOnUtc: domainEvent.OccurredOnUtc,
            companyId: domainEvent.CompanyId,
            stockItemId: domainEvent.StockItemId,
            consumedQuantity: domainEvent.ConsumedQuantity.Value,
            resultingQuantity: domainEvent.ResultingQuantity.Value,
            unit: domainEvent.ResultingQuantity.Unit.Symbol,
            allocations: BatchAllocationMapper.ToDtos(domainEvent.Allocations),
            occurredOn: domainEvent.OccurredOn,
            experimentId: domainEvent.ExperimentId);
}
