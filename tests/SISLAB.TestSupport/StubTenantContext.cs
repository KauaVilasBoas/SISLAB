using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.TestSupport;

/// <summary>
/// An <see cref="ITenantContext"/> pinned to a fixed company, matching how the read/write side resolves the
/// active tenant. Shared by every test project instead of being re-declared (inline or per assembly).
/// </summary>
public sealed class StubTenantContext : ITenantContext
{
    public StubTenantContext(Guid companyId) => CompanyId = companyId;

    public Guid CompanyId { get; }
}
