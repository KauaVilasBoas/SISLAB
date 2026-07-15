using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Contracts.Events;

/// <summary>
/// Public contract published when a new company (tenant) is created through self-service signup
/// (card [E12] #75a). It is the cross-module fact "a brand-new tenant now exists" that downstream
/// bounded contexts react to — most notably the Configuration module, which provisions the tenant's
/// baseline configuration (default expiry policy + base categories + base units, card #75b) on receipt.
///
/// <para>Delivered via the Transactional Outbox (§6): the Identity write-side raises the internal
/// <c>CompanyCreated</c> domain event, translates it into this flattened contract and enqueues it in the
/// <c>tenancy.outbox_messages</c> table inside the signup transaction. The background Outbox dispatcher
/// then publishes it through <see cref="IEventBus"/> after commit, so a failure in provisioning never
/// rolls back (or blocks) the signup itself — it is simply retried until it succeeds.</para>
///
/// <para>Flattened by design: it carries only primitives (<see cref="CompanyId"/> held by value), so a
/// consumer never depends on the Identity domain. <see cref="CompanyId"/> is the id the consumer scopes
/// its reaction to; it doubles as the natural idempotency key for the provisioning handler.</para>
/// </summary>
public sealed record CompanyCreatedIntegrationEvent : IIntegrationEvent
{
    public CompanyCreatedIntegrationEvent(
        Guid eventId,
        DateTime occurredOnUtc,
        Guid companyId,
        string name,
        Guid coordinatorUserId)
    {
        EventId = eventId;
        OccurredOnUtc = occurredOnUtc;
        CompanyId = companyId;
        Name = name;
        CoordinatorUserId = coordinatorUserId;
    }

    /// <inheritdoc />
    public Guid EventId { get; }

    /// <inheritdoc />
    public DateTime OccurredOnUtc { get; }

    /// <inheritdoc />
    public string EventType => nameof(CompanyCreatedIntegrationEvent);

    /// <summary>Identity of the newly created company/tenant — the scope every consumer reacts within.</summary>
    public Guid CompanyId { get; }

    /// <summary>The company's display name.</summary>
    public string Name { get; }

    /// <summary>Lumen user id of the founding coordinator, referenced by value (no cross-module FK).</summary>
    public Guid CoordinatorUserId { get; }
}
