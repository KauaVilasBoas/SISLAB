using SISLAB.Modules.Identity.Domain.Invitations.Events;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Identity.Domain.Invitations;

/// <summary>
/// Aggregate root for a member invitation (card [E12] #75c): a coordinator invites someone to a
/// <see cref="Companies.Company"/> by e-mail, choosing the Lumen profile the invitee receives on accept.
///
/// <para><b>Invariants (owned here, not in a handler):</b></para>
/// <list type="bullet">
///   <item>e-mail and profile are always present; e-mail is stored normalized;</item>
///   <item>the accept token is never stored raw — only its hash (<see cref="InvitationToken"/>);</item>
///   <item>an invitation can be accepted at most once, and only while <see cref="InvitationStatus.Pending"/>
///     and not past <see cref="ExpiresAt"/> — otherwise the transition is refused;</item>
///   <item>a presented token is verified by hash, in constant time.</item>
/// </list>
///
/// <para>The "at most one <see cref="InvitationStatus.Pending"/> per (company, e-mail)" rule is a set-level
/// invariant enforced by a partial unique index and by the invite use case rehydrating an existing pending
/// invitation via <see cref="Reissue"/> (resend) instead of creating a duplicate — so a resend is idempotent.</para>
/// </summary>
public sealed class CompanyInvitation : AggregateRoot<Guid>
{
    /// <summary>Default lifetime of an invitation before it can no longer be accepted (card decision: 7 days).</summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);

    // Private constructor for EF Core
    private CompanyInvitation() : base(Guid.Empty) { }

    private CompanyInvitation(
        Guid id,
        Guid companyId,
        string email,
        Guid profileId,
        InvitationToken token,
        Guid invitedByUserId,
        DateTime createdAt,
        DateTime expiresAt)
        : base(id)
    {
        CompanyId = companyId;
        Email = email;
        ProfileId = profileId;
        Token = token;
        InvitedByUserId = invitedByUserId;
        Status = InvitationStatus.Pending;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid CompanyId { get; private init; }

    /// <summary>Invitee e-mail, normalized (trimmed, lower-invariant) so the (company, e-mail) key is stable.</summary>
    public string Email { get; private set; } = default!;

    /// <summary>Lumen profile the invitee is granted (company-scoped) when they accept — referenced by value.</summary>
    public Guid ProfileId { get; private set; }

    /// <summary>The hashed accept token. The raw token exists only in the issuing <see cref="MemberInvited"/> event.</summary>
    public InvitationToken Token { get; private set; } = default!;

    public InvitationStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public DateTime? AcceptedAt { get; private set; }

    /// <summary>Lumen user id of the coordinator who issued the invitation — referenced by value.</summary>
    public Guid InvitedByUserId { get; private init; }

    /// <summary>
    /// Issues a fresh pending invitation and raises <see cref="MemberInvited"/> carrying the raw token so the
    /// e-mail can be built downstream. The raw token is generated here and never stored — only its hash lives
    /// on the aggregate.
    /// </summary>
    /// <param name="companyId">Company the invitee is invited into; must not be empty.</param>
    /// <param name="email">Invitee e-mail; must not be empty (stored normalized).</param>
    /// <param name="profileId">Lumen profile granted on accept; must not be empty.</param>
    /// <param name="invitedByUserId">Coordinator issuing the invitation; must not be empty.</param>
    /// <param name="clock">Time source for <c>CreatedAt</c>/<c>ExpiresAt</c> (testable).</param>
    /// <param name="lifetime">Optional lifetime override; defaults to <see cref="DefaultLifetime"/>.</param>
    public static CompanyInvitation Issue(
        Guid companyId,
        string email,
        Guid profileId,
        Guid invitedByUserId,
        IClock clock,
        TimeSpan? lifetime = null)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("Company id cannot be empty.", nameof(companyId));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Invitation e-mail cannot be empty.", nameof(email));
        if (profileId == Guid.Empty)
            throw new ArgumentException("Profile id cannot be empty.", nameof(profileId));
        if (invitedByUserId == Guid.Empty)
            throw new ArgumentException("Inviter user id cannot be empty.", nameof(invitedByUserId));
        ArgumentNullException.ThrowIfNull(clock);

        DateTime now = clock.UtcNow;
        (string rawToken, InvitationToken token) = InvitationToken.Generate();

        var invitation = new CompanyInvitation(
            Guid.NewGuid(),
            companyId,
            NormalizeEmail(email),
            profileId,
            token,
            invitedByUserId,
            createdAt: now,
            expiresAt: now.Add(lifetime ?? DefaultLifetime));

        invitation.RaiseInvited(rawToken);
        return invitation;
    }

    /// <summary>
    /// Re-issues an existing pending invitation (resend): mints a new token, resets the expiry window and the
    /// chosen profile, and raises <see cref="MemberInvited"/> again with the new raw token. Idempotent by design
    /// — a resend reuses the same aggregate instead of creating a second pending invitation for the same
    /// (company, e-mail), which the partial unique index also forbids.
    /// </summary>
    /// <exception cref="ConflictException">The invitation is no longer pending (already accepted/revoked).</exception>
    public void Reissue(Guid profileId, IClock clock, TimeSpan? lifetime = null)
    {
        if (profileId == Guid.Empty)
            throw new ArgumentException("Profile id cannot be empty.", nameof(profileId));
        ArgumentNullException.ThrowIfNull(clock);
        if (Status != InvitationStatus.Pending)
            throw new ConflictException(
                $"Invitation '{Id}' cannot be re-sent because it is '{Status}', not pending.");

        DateTime now = clock.UtcNow;
        (string rawToken, InvitationToken token) = InvitationToken.Generate();

        ProfileId = profileId;
        Token = token;
        CreatedAt = now;
        ExpiresAt = now.Add(lifetime ?? DefaultLifetime);

        RaiseInvited(rawToken);
    }

    /// <summary>
    /// Accepts the invitation, transitioning it to <see cref="InvitationStatus.Accepted"/>. The caller must have
    /// already verified the presented token via <see cref="MatchesToken"/>. Refuses if the invitation is not
    /// pending or has expired (lazy expiry: an accept attempt past <see cref="ExpiresAt"/> flips it to
    /// <see cref="InvitationStatus.Expired"/> and is rejected — there is no sweep job).
    /// </summary>
    /// <exception cref="ConflictException">Invitation is not pending (e.g. already accepted — double accept).</exception>
    /// <exception cref="BusinessException">Invitation has expired.</exception>
    public void Accept(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (Status != InvitationStatus.Pending)
            throw new ConflictException(
                $"Invitation '{Id}' cannot be accepted because it is '{Status}', not pending.");

        if (IsExpired(clock.UtcNow))
        {
            Status = InvitationStatus.Expired;
            throw new BusinessException("This invitation has expired.");
        }

        Status = InvitationStatus.Accepted;
        AcceptedAt = clock.UtcNow;
    }

    /// <summary>Cancels a pending invitation. No-op semantics live in the use case; here it enforces the state guard.</summary>
    /// <exception cref="ConflictException">Invitation is not pending.</exception>
    public void Revoke()
    {
        if (Status != InvitationStatus.Pending)
            throw new ConflictException(
                $"Invitation '{Id}' cannot be revoked because it is '{Status}', not pending.");

        Status = InvitationStatus.Revoked;
    }

    /// <summary>Whether the presented raw token matches this invitation's stored hash (constant-time).</summary>
    public bool MatchesToken(string rawToken) => Token.Matches(rawToken);

    /// <summary>Whether the invitation is past its acceptance window at the given instant.</summary>
    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAt;

    /// <summary>True while the invitation can still be accepted at the given instant.</summary>
    public bool IsAcceptable(DateTime nowUtc) => Status == InvitationStatus.Pending && !IsExpired(nowUtc);

    private void RaiseInvited(string rawToken) =>
        RaiseDomainEvent(new MemberInvited(Id, CompanyId, Email, ProfileId, InvitedByUserId, rawToken));

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
