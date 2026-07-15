namespace SISLAB.Modules.Identity.Domain.Invitations;

/// <summary>
/// Lifecycle state of a <see cref="CompanyInvitation"/> (card [E12] #75c).
///
/// <para>An invitation is born <see cref="Pending"/> and can only leave that state once: it is
/// <see cref="Accepted"/> when the invitee joins, <see cref="Revoked"/> if a coordinator cancels it, or
/// <see cref="Expired"/> when its window elapses. The transition guard lives in the aggregate — the enum
/// only names the states.</para>
/// </summary>
public enum InvitationStatus
{
    /// <summary>Outstanding: the invitee may still accept it (if not past <c>ExpiresAt</c>).</summary>
    Pending = 0,

    /// <summary>Consumed: the invitee joined the company. Terminal.</summary>
    Accepted = 1,

    /// <summary>Cancelled by a coordinator before being accepted. Terminal.</summary>
    Revoked = 2,

    /// <summary>Marked past its window. Terminal (lazy — set on an accept attempt after expiry).</summary>
    Expired = 3,
}
