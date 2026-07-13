using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Infrastructure.Multitenancy;

/// <summary>
/// The <b>effective</b> <see cref="ITenantContext"/> every tenant-scoped consumer resolves — a thin
/// composition (Decorator) over the request-resolved tenant and the background <see cref="ITenantContextOverride"/>.
///
/// <para>
/// Precedence: when a job has set an override for the current scope, <see cref="CompanyId"/> reports the
/// overriding company; otherwise it falls back to the inner request context — so on the HTTP path, where no
/// override is ever set, this is behaviourally identical to resolving the raw request <see cref="ITenantContext"/>.
/// The seam is thus invisible to normal request handling and adds the cross-tenant scan capability without
/// touching any query or the tenant-resolution middleware.
/// </para>
///
/// <para>
/// Composed in the Identity module's DI (which owns the request tenant source): the concrete request
/// <c>TenantContext</c> is supplied as <paramref name="requestContext"/> and this type is exposed as the
/// public <see cref="ITenantContext"/>. Scoped, matching both collaborators' lifetime.
/// </para>
/// </summary>
public sealed class OverridableTenantContext : ITenantContext
{
    private readonly ITenantContext _requestContext;
    private readonly ITenantContextOverride _override;

    public OverridableTenantContext(ITenantContext requestContext, ITenantContextOverride tenantOverride)
    {
        _requestContext = requestContext;
        _override = tenantOverride;
    }

    /// <inheritdoc />
    public Guid CompanyId => _override.CompanyId ?? _requestContext.CompanyId;
}
