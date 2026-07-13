using SISLAB.Infrastructure.Multitenancy;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Infrastructure.Tests.Multitenancy;

/// <summary>
/// Proves the E6 tenant-override seam (Fork #1 → A): the effective <see cref="ITenantContext"/>
/// (<see cref="OverridableTenantContext"/>) reports the background <see cref="ITenantContextOverride"/> when a
/// job has set one, and otherwise falls back to the request-resolved tenant EXACTLY as today. This is what
/// lets the alert jobs (#41/#42/#66) reuse the E4 read queries intact — the queries read the effective
/// context, which the job steers per company — while the HTTP path is untouched (no override is ever set there).
/// </summary>
public sealed class OverridableTenantContextTests
{
    private static readonly Guid RequestCompany = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OverrideCompany = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Without_an_override_the_effective_context_is_the_request_tenant()
    {
        TenantContextOverride tenantOverride = new();
        OverridableTenantContext effective = new(new StubRequestContext(RequestCompany), tenantOverride);

        // No override set — HTTP behaviour is exactly the raw request tenant.
        Assert.Equal(RequestCompany, effective.CompanyId);
    }

    [Fact]
    public void With_an_override_the_effective_context_reports_the_overriding_company()
    {
        TenantContextOverride tenantOverride = new();
        OverridableTenantContext effective = new(new StubRequestContext(RequestCompany), tenantOverride);

        tenantOverride.SetCompany(OverrideCompany);

        Assert.Equal(OverrideCompany, effective.CompanyId);
    }

    [Fact]
    public void Clearing_the_override_restores_the_request_fallback()
    {
        TenantContextOverride tenantOverride = new();
        OverridableTenantContext effective = new(new StubRequestContext(RequestCompany), tenantOverride);

        tenantOverride.SetCompany(OverrideCompany);
        tenantOverride.Clear();

        Assert.Equal(RequestCompany, effective.CompanyId);
    }

    [Fact]
    public void The_override_works_even_when_there_is_no_request_tenant()
    {
        // A background job has no request tenant (Guid.Empty); the override alone drives the effective company.
        TenantContextOverride tenantOverride = new();
        OverridableTenantContext effective = new(new StubRequestContext(Guid.Empty), tenantOverride);

        tenantOverride.SetCompany(OverrideCompany);

        Assert.Equal(OverrideCompany, effective.CompanyId);
    }

    [Fact]
    public void Setting_an_empty_company_is_rejected()
    {
        TenantContextOverride tenantOverride = new();

        Assert.Throws<ArgumentException>(() => tenantOverride.SetCompany(Guid.Empty));
    }

    private sealed class StubRequestContext : ITenantContext
    {
        public StubRequestContext(Guid companyId) => CompanyId = companyId;
        public Guid CompanyId { get; }
    }
}
