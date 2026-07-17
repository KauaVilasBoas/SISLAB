using SISLAB.Modules.Inventory.Contracts.Events;
using SISLAB.Modules.Inventory.Domain.StockItems.Events;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Infrastructure.Messaging;

/// <summary>
/// Translates the internal <see cref="StockDisposedEvent"/> into the public
/// <see cref="StockDisposedIntegrationEvent"/> before it is written to the Outbox.
/// </summary>
internal sealed class StockDisposedEventTranslator
    : IDomainEventToIntegrationEventTranslator<StockDisposedEvent>
{
    public IIntegrationEvent Translate(StockDisposedEvent domainEvent) =>
        new StockDisposedIntegrationEvent(
            eventId: Guid.NewGuid(),
            occurredOnUtc: domainEvent.OccurredOnUtc,
            companyId: domainEvent.CompanyId,
            stockItemId: domainEvent.StockItemId,
            disposedQuantity: domainEvent.DisposedQuantity.Value,
            resultingQuantity: domainEvent.ResultingQuantity.Value,
            unit: domainEvent.ResultingQuantity.Unit.Symbol,
            allocations: BatchAllocationMapper.ToDtos(domainEvent.Allocations),
            occurredOn: domainEvent.OccurredOn);
}
