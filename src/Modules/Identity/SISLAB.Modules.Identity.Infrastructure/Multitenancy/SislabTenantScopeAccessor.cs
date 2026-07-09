using Lumen.Authorization.Contracts;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Identity.Infrastructure.Multitenancy;

/// <summary>
/// Implements Lumen's <see cref="ITenantScopeAccessor"/> by delegating to SISLAB's <see cref="ITenantContext"/>.
///
/// Lumen uses the scope returned here to filter permissions by tenant:
/// <c>GetPermissionCodesByUserIdAsync(userId, scopeId)</c> returns only the permissions
/// the user holds within the active company.
///
/// Registration: overrides Lumen's no-op (registered via TryAdd) — must be registered
/// AFTER all Lumen wiring (AddLumenIdentity / AddLumenAuthorization) to win the override.
/// Scoped — follows the HTTP request lifetime, same as ITenantContext.
/// </summary>
internal sealed class SislabTenantScopeAccessor : ITenantScopeAccessor
{
    private readonly ITenantContext _tenantContext;

    public SislabTenantScopeAccessor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Returns the active company id as the Lumen authorization scope.
    /// Unauthenticated routes or requests without an active tenant return null (global scope).
    /// </summary>
    public Guid? GetCurrentScopeId()
    {
        try
        {
            Guid companyId = _tenantContext.CompanyId;
            return companyId == Guid.Empty ? null : companyId;
        }
        catch
        {
            // ITenantContext may throw outside authenticated routes; treat as no scope.
            return null;
        }
    }
}
