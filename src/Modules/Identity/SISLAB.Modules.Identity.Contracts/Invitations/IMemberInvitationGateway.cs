namespace SISLAB.Modules.Identity.Contracts.Invitations;

/// <summary>
/// Anti-corruption seam (Adapter/Facade) over Lumen Identity for the invitation-accept flow (card [E12] #75c):
/// the user-provisioning steps accepting an invitation needs — <b>find</b> an existing Lumen user by e-mail and
/// <b>create</b> a new one when the invitee has no account yet — behind one port whose implementation lives in
/// the module's Infrastructure (§8).
///
/// <para>Kept separate from <see cref="Onboarding.ICompanyOnboardingGateway"/> (which is signup-specific and
/// owns the coordinator profile) so each seam stays cohesive: this one exists solely for accepting invitations.
/// The <c>AcceptInvitationCommand</c> handler depends on this abstraction, never on Lumen directly, so it stays
/// unit-testable and Lumen stays confined to Infrastructure.</para>
///
/// <para><b>Fork 1 (link existing account):</b> when the invitee's e-mail already belongs to a Lumen user, the
/// accept flow reuses that user (no password prompt) — it only creates the new company membership and grants
/// the profile in the new company, honouring the N:N "one user, many companies" model (§7). A new account is
/// created only when no user exists for the e-mail.</para>
/// </summary>
public interface IMemberInvitationGateway
{
    /// <summary>
    /// Returns the id of the Lumen user with the given e-mail (case-insensitive), or <see langword="null"/> when
    /// none exists. Used on accept to decide between linking the existing account (Fork 1) and creating one.
    /// </summary>
    Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new Lumen user for an invitee that has no account yet, already active (e-mail confirmed) so
    /// they can authenticate immediately after accepting, and returns the new Lumen user id. The password is set
    /// here and must satisfy Lumen's password policy; it is hashed by the adapter before storage, never persisted
    /// raw.
    /// </summary>
    /// <param name="email">Invitee e-mail (login).</param>
    /// <param name="username">Invitee display/user name.</param>
    /// <param name="password">Plain-text password, hashed by the adapter before storage.</param>
    Task<Guid> CreateInvitedUserAsync(
        string email,
        string username,
        string password,
        CancellationToken cancellationToken = default);
}
