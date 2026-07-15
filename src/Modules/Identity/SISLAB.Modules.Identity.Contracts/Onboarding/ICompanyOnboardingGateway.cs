namespace SISLAB.Modules.Identity.Contracts.Onboarding;

/// <summary>
/// Anti-corruption seam (Adapter/Facade) over Lumen for the self-service signup flow (card [E12] #75a): the
/// two Lumen-owned steps a company signup needs — provisioning the coordinator <b>user</b> (Lumen Identity)
/// and granting that user the coordinator's <b>authorization</b> scoped to the new company (Lumen
/// Authorization) — behind one port whose implementation lives in the module's Infrastructure (§8).
///
/// <para>Keeping these behind a dedicated port (rather than inflating <c>ILumenAuthorizationGateway</c>) keeps
/// each seam cohesive: this one exists solely for onboarding and is the only place that creates a coordinator
/// account. The <see cref="SignupCompanyCommand"/> handler depends on this abstraction, never on Lumen or
/// MediatR directly, so the write-side handler stays unit-testable and Lumen stays confined to Infrastructure.</para>
///
/// <para>SISLAB models no roles: "coordinator" is materialized entirely as a company-scoped Lumen profile
/// assignment (<see cref="GrantCoordinatorAccessAsync"/>), consistent with the rest of the module.</para>
/// </summary>
public interface ICompanyOnboardingGateway
{
    /// <summary>
    /// Whether a Lumen Identity user already exists with the given e-mail (case-insensitive). Used by signup to
    /// reject a duplicate coordinator before anything is created, so the flow fails fast with a conflict.
    /// </summary>
    Task<bool> CoordinatorEmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the coordinator user in Lumen Identity, already active (e-mail confirmed) so the coordinator can
    /// authenticate immediately after signup, and returns the new Lumen user id. The password is set at signup;
    /// it must satisfy Lumen's password policy. Persistence is committed to Lumen Identity's own store.
    /// </summary>
    /// <param name="email">Coordinator e-mail (login).</param>
    /// <param name="username">Coordinator display/user name.</param>
    /// <param name="password">Plain-text password, hashed by the adapter before storage; never persisted raw.</param>
    Task<Guid> CreateCoordinatorAsync(
        string email,
        string username,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants the coordinator full access to the newly created company by assigning the coordinator profile
    /// scoped to that company (<c>ScopeId = companyId</c>), idempotently. The profile is ensured by the adapter
    /// (created on first use) and the grant applies inside this tenant only — the same user may hold different
    /// profiles in other companies.
    /// </summary>
    Task GrantCoordinatorAccessAsync(
        Guid coordinatorUserId,
        Guid companyId,
        CancellationToken cancellationToken = default);
}
