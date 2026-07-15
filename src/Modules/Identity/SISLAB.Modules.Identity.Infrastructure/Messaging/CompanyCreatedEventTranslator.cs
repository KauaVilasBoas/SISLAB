using SISLAB.Modules.Identity.Contracts.Events;
using SISLAB.Modules.Identity.Domain.Companies.Events;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Infrastructure.Messaging;

/// <summary>
/// Translates the internal <see cref="CompanyCreated"/> domain event into the public
/// <see cref="CompanyCreatedIntegrationEvent"/> before it is written to the Outbox (card [E12] #75b).
/// The <see cref="DomainEventDispatcher"/> resolves this translator by domain-event type during
/// SaveChanges and enqueues the flattened contract into <c>tenancy.outbox_messages</c>, in the signup
/// transaction. A fresh <see cref="Guid"/> is minted as the event id so it is a stable idempotency key
/// for the Outbox and downstream consumers.
/// </summary>
internal sealed class CompanyCreatedEventTranslator
    : IDomainEventToIntegrationEventTranslator<CompanyCreated>
{
    public IIntegrationEvent Translate(CompanyCreated domainEvent) =>
        new CompanyCreatedIntegrationEvent(
            eventId: Guid.NewGuid(),
            occurredOnUtc: domainEvent.OccurredOnUtc,
            companyId: domainEvent.CompanyId,
            name: domainEvent.Name,
            coordinatorUserId: domainEvent.CoordinatorUserId);
}
