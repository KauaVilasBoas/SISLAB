using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Jobs.Multitenancy;

/// <summary>
/// Settable <see cref="ITenantContext"/> for the jobs host (E6 #39, Fork #2 → A).
///
/// In the API, <c>ITenantContext</c> is a scoped, request-bound value populated by
/// <c>TenantResolutionMiddleware</c> from the httpOnly cookie. Background jobs have no HTTP request,
/// so they need a different source for the active company: a per-scope value the job itself sets.
///
/// This implementation is registered <b>Scoped</b> and the scheduling base
/// (<c>TimedBackgroundService</c>) creates one DI scope per tick, so each iteration gets its own
/// instance. The company-scanning alert jobs (#41/#42/#66) will, inside a tenant-bypass scope, loop
/// over companies and call <see cref="SetCompany"/> before dispatching the E4 read queries — reusing
/// those queries INTACT, since they read <c>CompanyId</c> from this same abstraction.
///
/// <para>
/// This card (#39) only wires the infrastructure; the per-company scan lands in the alert cards.
/// </para>
/// </summary>
public sealed class AmbientTenantContext : ITenantContext
{
    /// <inheritdoc />
    public Guid CompanyId { get; private set; } = Guid.Empty;

    /// <summary>
    /// Sets the active company for the current tick's scope. Called by a job that has already opened
    /// an auditable <see cref="ITenantBypass"/> scope and is iterating companies. Passing
    /// <see cref="Guid.Empty"/> clears the company.
    /// </summary>
    public void SetCompany(Guid companyId) => CompanyId = companyId;

    /// <summary>Clears the active company, restoring the neutral (no-tenant) state.</summary>
    public void Clear() => CompanyId = Guid.Empty;
}
