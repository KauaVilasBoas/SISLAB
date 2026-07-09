using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Identity.Infrastructure.Multitenancy;

/// <summary>
/// Mutable, concrete implementation of <see cref="ITenantContext"/> — Scoped (one instance per HTTP request).
///
/// <see cref="TenantResolutionMiddleware"/> resolves the active company from the httpOnly cookie,
/// validates membership, and calls <see cref="SetCompany"/>. On public routes or when no valid cookie
/// is present, <see cref="CompanyId"/> stays <see cref="Guid.Empty"/> and
/// <see cref="SislabTenantScopeAccessor"/> returns a null scope (no active tenant).
/// </summary>
internal sealed class TenantContext : ITenantContext
{
    /// <inheritdoc />
    public Guid CompanyId { get; private set; } = Guid.Empty;

    public bool HasCompany => CompanyId != Guid.Empty;

    /// <summary>
    /// Sets the active company for this request.
    /// Called exclusively by <see cref="TenantResolutionMiddleware"/> after membership validation.
    /// </summary>
    public void SetCompany(Guid companyId)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("Active CompanyId cannot be empty.", nameof(companyId));

        CompanyId = companyId;
    }
}
