namespace SISLAB.Modules.Identity.Contracts.Invitations;

/// <summary>
/// Public flattened preview of a member invitation, returned by the anonymous preview endpoint (card [E12]
/// #75c) so the SPA can show <i>what</i> is being accepted (which company, e-mail and profile) before the
/// invitee commits — and whether the account already exists, which decides if the accept form must collect a
/// username/password.
///
/// <para>References only primitives and human-readable labels — never the invitation aggregate or the raw token
/// (the token stays in the URL the invitee already holds). Exposes no ids that would let a caller enumerate or
/// target other tenants.</para>
/// </summary>
/// <param name="Email">The invitee e-mail the invitation was issued to.</param>
/// <param name="CompanyName">Display name of the company the invitee is invited into.</param>
/// <param name="ProfileName">Display name of the Lumen profile the invitee will receive on accept.</param>
/// <param name="ExpiresAt">When the invitation stops being acceptable (UTC).</param>
/// <param name="Status">Current lifecycle status label (<c>Pending</c>/<c>Accepted</c>/<c>Revoked</c>/<c>Expired</c>).</param>
/// <param name="Acceptable">True only when the invitation can still be accepted right now (pending and not expired).</param>
/// <param name="RequiresAccountCreation">
/// True when the e-mail has no Lumen account yet, so the accept form must collect a username and password;
/// false when accepting will simply link the existing account (Fork 1 — no password prompt).
/// </param>
public sealed record InvitationPreviewDto(
    string Email,
    string CompanyName,
    string ProfileName,
    DateTime ExpiresAt,
    string Status,
    bool Acceptable,
    bool RequiresAccountCreation);
