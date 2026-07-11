using SISLAB.Modules.Inventory.Contracts.Events;
using SISLAB.Modules.Inventory.Domain.StockItems.Events;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Infrastructure.Messaging;

/// <summary>
/// Translates the internal <see cref="StockBelowMinimumEvent"/> into the public
/// <see cref="StockBelowMinimumIntegrationEvent"/> before it is written to the Outbox. This is the
/// trigger the low-stock alert job (E6) consumes.
/// </summary>
internal sealed class StockBelowMinimumEventTranslator
    : IDomainEventToIntegrationEventTranslator<StockBelowMinimumEvent>
{
    public IIntegrationEvent Translate(StockBelowMinimumEvent domainEvent) =>
        new StockBelowMinimumIntegrationEvent(
            eventId: Guid.NewGuid(),
            occurredOnUtc: domainEvent.OccurredOnUtc,
            companyId: domainEvent.CompanyId,
            stockItemId: domainEvent.StockItemId,
            currentQuantity: domainEvent.CurrentQuantity.Value,
            minimumQuantity: domainEvent.MinimumQuantity.Value,
            unit: domainEvent.CurrentQuantity.Unit.Symbol);
}
