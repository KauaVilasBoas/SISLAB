using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Contracts.Events;

/// <summary>
/// Public contract published when a coordinator invites someone to a company by e-mail (card [E12] #75c). It
/// is the fact "an invitation was issued and its e-mail must be delivered".
///
/// <para>Delivered via the Transactional Outbox (§6): the Identity write-side raises the internal
/// <c>MemberInvited</c> domain event, translates it into this flattened contract and enqueues it in
/// <c>tenancy.outbox_messages</c> inside the invite transaction. The background Outbox dispatcher publishes it
/// through <see cref="IEventBus"/> after commit, so the invitation e-mail is an eventual, retried side effect —
/// a mail-delivery fault never rolls back (or blocks) the invitation itself.</para>
///
/// <para>The only consumer is the Identity module's own e-mail handler (same bounded context): it translates
/// the event into a branded <c>MemberInvitation</c> e-mail and sends it. <see cref="RawToken"/> is the
/// single-use accept secret — it lives only in transit here and in the outgoing e-mail link; the aggregate
/// stores only its hash. It is carried flattened (primitives only) so no consumer depends on the Identity
/// domain.</para>
/// </summary>
public sealed record MemberInvitedIntegrationEvent : IIntegrationEvent
{
    public MemberInvitedIntegrationEvent(
        Guid eventId,
        DateTime occurredOnUtc,
        Guid invitationId,
        Guid companyId,
        string email,
        Guid profileId,
        Guid invitedByUserId,
        string rawToken)
    {
        EventId = eventId;
        OccurredOnUtc = occurredOnUtc;
        InvitationId = invitationId;
        CompanyId = companyId;
        Email = email;
        ProfileId = profileId;
        InvitedByUserId = invitedByUserId;
        RawToken = rawToken;
    }

    /// <inheritdoc />
    public Guid EventId { get; }

    /// <inheritdoc />
    public DateTime OccurredOnUtc { get; }

    /// <inheritdoc />
    public string EventType => nameof(MemberInvitedIntegrationEvent);

    /// <summary>Identity of the issued invitation.</summary>
    public Guid InvitationId { get; }

    /// <summary>Company (tenant) the invitee is invited into, referenced by value.</summary>
    public Guid CompanyId { get; }

    /// <summary>Normalized invitee e-mail — the recipient of the invitation e-mail.</summary>
    public string Email { get; }

    /// <summary>Lumen profile the invitee will receive on accept, referenced by value.</summary>
    public Guid ProfileId { get; }

    /// <summary>Lumen user id of the coordinator who issued the invitation, referenced by value.</summary>
    public Guid InvitedByUserId { get; }

    /// <summary>The plaintext accept token embedded in the e-mail link. Single-use; never persisted raw.</summary>
    public string RawToken { get; }
}
