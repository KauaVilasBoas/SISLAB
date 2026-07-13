using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Infrastructure.Multitenancy;

/// <summary>
/// Scoped implementation of <see cref="ITenantContextOverride"/> — the write seam a background job uses to
/// make a company the active tenant for the current DI scope (E6 alert jobs #41/#42/#66).
///
/// Registered <b>Scoped</b> so the override set inside one company iteration never bleeds into the next
/// iteration or into any HTTP request. It carries no behaviour of its own beyond holding the overriding
/// company; the effective tenant context (<see cref="OverridableTenantContext"/>) reads it and, when set,
/// reports it in place of the request-resolved tenant.
/// </summary>
public sealed class TenantContextOverride : ITenantContextOverride
{
    /// <inheritdoc />
    public Guid? CompanyId { get; private set; }

    /// <inheritdoc />
    public void SetCompany(Guid companyId)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("Override CompanyId cannot be empty.", nameof(companyId));

        CompanyId = companyId;
    }

    /// <inheritdoc />
    public void Clear() => CompanyId = null;
}
