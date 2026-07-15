using SISLAB.Modules.Identity.Contracts.Events;
using SISLAB.Modules.Identity.Domain.Invitations.Events;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Infrastructure.Messaging;

/// <summary>
/// Translates the internal <see cref="MemberInvited"/> domain event into the public
/// <see cref="MemberInvitedIntegrationEvent"/> before it is written to the Outbox (card [E12] #75c). The
/// <see cref="DomainEventDispatcher"/> resolves this translator by domain-event type during SaveChanges and
/// enqueues the flattened contract into <c>tenancy.outbox_messages</c>, in the invite transaction. A fresh
/// <see cref="System.Guid"/> is minted as the event id so it is a stable idempotency key for the Outbox and the
/// downstream e-mail handler. The raw token is carried through so the e-mail link can embed it.
/// </summary>
internal sealed class MemberInvitedEventTranslator
    : IDomainEventToIntegrationEventTranslator<MemberInvited>
{
    public IIntegrationEvent Translate(MemberInvited domainEvent) =>
        new MemberInvitedIntegrationEvent(
            eventId: Guid.NewGuid(),
            occurredOnUtc: domainEvent.OccurredOnUtc,
            invitationId: domainEvent.InvitationId,
            companyId: domainEvent.CompanyId,
            email: domainEvent.Email,
            profileId: domainEvent.ProfileId,
            invitedByUserId: domainEvent.InvitedByUserId,
            rawToken: domainEvent.RawToken);
}
