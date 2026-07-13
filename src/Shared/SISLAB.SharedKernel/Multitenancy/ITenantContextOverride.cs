namespace SISLAB.SharedKernel.Multitenancy;

/// <summary>
/// Settable seam that overrides the active company of the <b>effective</b> <see cref="ITenantContext"/>
/// for system/background work that has no HTTP request (E6 alert jobs #41/#42/#66).
///
/// <para>
/// In the API, <see cref="ITenantContext"/> is the request-scoped value the tenant-resolution middleware
/// populates from the httpOnly cookie. Background jobs iterate <b>every</b> company under an auditable
/// <see cref="ITenantBypass"/> scope and, for each one, need the read-side queries to see that company as
/// the active tenant — <b>without</b> passing the company id through every query signature. This override
/// is the seam that lets them do so: a job sets the company here, and the effective tenant context reports
/// it instead of the (absent) request context. When no override is set the effective context behaves
/// EXACTLY as it does on the HTTP path (it falls back to the request-resolved tenant), so the seam is
/// invisible to normal request handling.
/// </para>
///
/// <para>
/// Registered <b>Scoped</b>: the scheduling base opens a fresh DI scope per company iteration, so the
/// override never leaks between companies or into unrelated requests. It is deliberately a distinct
/// abstraction from <see cref="ITenantContext"/> (read-only for consumers) — only code that legitimately
/// drives the tenant (a job) depends on this write seam.
/// </para>
/// </summary>
public interface ITenantContextOverride
{
    /// <summary>
    /// The overriding company, or <see langword="null"/> when no override is in effect (the effective
    /// tenant context then falls back to the request-resolved company).
    /// </summary>
    Guid? CompanyId { get; }

    /// <summary>
    /// Sets the overriding company for the current scope. Called by a background job that has already
    /// opened an auditable <see cref="ITenantBypass"/> scope and is iterating companies.
    /// </summary>
    /// <param name="companyId">The company to make active; must not be <see cref="Guid.Empty"/>.</param>
    void SetCompany(Guid companyId);

    /// <summary>Clears the override, restoring the effective context's fallback (request) behaviour.</summary>
    void Clear();
}
