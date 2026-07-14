using SISLAB.SharedKernel.Authorization;

namespace SISLAB.Modules.Identity.Domain.Companies;

/// <summary>
/// Port that reconciles a member's authorization profile with their business <see cref="Role"/>,
/// scoped to a company (card [E12] #77d). This is the bridge between SISLAB's RBAC concept
/// (<see cref="CompanyMembership.Role"/>) and the underlying authorization system's profile model.
///
/// <para>The interface is expressed purely in domain primitives (<see cref="Guid"/> ids and
/// <see cref="Role"/>) — it never leaks the authorization vendor's types — so the Domain and
/// Application layers can depend on it without knowing the implementation touches Lumen. The
/// concrete implementation lives in the module's Infrastructure, alongside the other Lumen wiring.</para>
///
/// <para>Contract: implementations MUST be idempotent. Reconciling a member already assigned the
/// profile that matches <paramref name="role"/> in the given company is a no-op, so replaying the
/// same reconciliation (e.g. a retried command, or a no-op role change) never accumulates stale
/// profile assignments. Isolation is guaranteed by scoping every assignment to <c>companyId</c>:
/// a member's permissions in company A never leak into company B.</para>
/// </summary>
public interface IMemberAuthorizationProfileService
{
    /// <summary>
    /// Ensures <paramref name="lumenUserId"/> holds exactly the authorization profile that corresponds to
    /// <paramref name="role"/> within <paramref name="companyId"/>, removing any other SISLAB role profile
    /// the user previously held in that same company. Idempotent.
    /// </summary>
    /// <param name="lumenUserId">The user (Lumen identity, by value) whose profile is being reconciled.</param>
    /// <param name="companyId">The company that scopes the profile assignment (authorization scope).</param>
    /// <param name="role">The business role whose permission set the user must end up with in that company.</param>
    Task ReconcileAsync(Guid lumenUserId, Guid companyId, Role role, CancellationToken cancellationToken = default);
}
