using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Identity.Domain.Invitations.Events;

/// <summary>
/// Raised when a coordinator invites someone to a company by e-mail (<see cref="CompanyInvitation.Issue"/>,
/// card [E12] #75c). It is the domain fact "an invitation now exists and its e-mail must go out".
///
/// <para>Downstream, the module's Infrastructure translates this into the public
/// <c>MemberInvitedIntegrationEvent</c> and writes it to the <c>tenancy</c> Outbox in the invite transaction
/// (§6). The background Outbox dispatcher then publishes it after commit, so the invitation e-mail is an
/// eventual, retried side effect — a mail-delivery fault never rolls back (or blocks) the invitation itself.</para>
///
/// <para>The event carries the <b>raw</b> token because it is the single moment the secret exists in plaintext:
/// the aggregate persists only the hash, so the e-mail (built downstream from this event) is the sole place the
/// raw token can be embedded into the accept link. The event is module-internal and rich; the raw token is
/// flattened onward only into the internal e-mail handler, never exposed on a query surface.</para>
/// </summary>
/// <param name="InvitationId">Identity of the invitation.</param>
/// <param name="CompanyId">Company (tenant) the invitee is being invited into.</param>
/// <param name="Email">Normalized invitee e-mail (the recipient of the invitation e-mail).</param>
/// <param name="ProfileId">Lumen profile the invitee will receive on accept.</param>
/// <param name="InvitedByUserId">Lumen user id of the coordinator who issued the invitation.</param>
/// <param name="RawToken">The plaintext accept token — embedded in the e-mail link, never persisted.</param>
public sealed record MemberInvited(
    Guid InvitationId,
    Guid CompanyId,
    string Email,
    Guid ProfileId,
    Guid InvitedByUserId,
    string RawToken) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
