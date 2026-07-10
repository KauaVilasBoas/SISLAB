namespace SISLAB.SharedKernel.Multitenancy;

/// <summary>
/// Tenant context for the current request (Option A: companyId outside the JWT).
/// The active company is resolved from an httpOnly cookie, validated per-request against
/// <c>company_memberships</c> by TenantResolutionMiddleware. Scoped — one instance per HTTP request.
///
/// CompanyId flow: httpOnly cookie → middleware validates membership → ITenantContext →
/// Command → EF Core global query filter → WHERE clause in Dapper.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Active company (tenant) for the current operation.
    /// Remains <see cref="Guid.Empty"/> on public routes, when no valid cookie is present,
    /// or when the user does not belong to the requested company.
    /// </summary>
    Guid CompanyId { get; }
}
