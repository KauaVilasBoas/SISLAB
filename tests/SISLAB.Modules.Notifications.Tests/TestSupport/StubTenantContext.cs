using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Notifications.Tests.TestSupport;

/// <summary>A tenant context pinned to a fixed company (or none), matching how the read/write side resolves it.</summary>
internal sealed class StubTenantContext : ITenantContext
{
    public StubTenantContext(Guid companyId) => CompanyId = companyId;

    public Guid CompanyId { get; }
}
