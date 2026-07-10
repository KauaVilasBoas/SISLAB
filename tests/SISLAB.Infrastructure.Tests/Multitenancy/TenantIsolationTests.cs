using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SISLAB.Infrastructure.Multitenancy;
using SISLAB.Infrastructure.Persistence;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Infrastructure.Tests.Multitenancy;

/// <summary>
/// Proves the write-side multi-tenancy machinery contributed by <see cref="SislabDbContextBase"/>:
/// the global query filter (read isolation), the tenant-stamping interceptor (auto company_id +
/// cross-tenant guard), and the auditable <see cref="ITenantBypass"/> escape hatch.
///
/// EF Core InMemory is used: it honors global query filters and runs save interceptors, which is
/// exactly what these behaviors rely on. A shared database name lets separate context instances
/// (one per simulated request/tenant) see the same store — mirroring real isolation.
/// </summary>
public sealed class TenantIsolationTests
{
    private static readonly Guid CompanyA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Query_returns_only_rows_of_the_active_company()
    {
        string dbName = Guid.NewGuid().ToString();

        // Seed one row per company (each through its own tenant scope).
        await using (var contextA = CreateContext(dbName, CompanyA))
        {
            contextA.Items.Add(TenantItem.New(Guid.NewGuid(), "A-item"));
            await contextA.SaveChangesAsync();
        }
        await using (var contextB = CreateContext(dbName, CompanyB))
        {
            contextB.Items.Add(TenantItem.New(Guid.NewGuid(), "B-item"));
            await contextB.SaveChangesAsync();
        }

        // Company A must never see Company B's data.
        await using var reader = CreateContext(dbName, CompanyA);
        List<TenantItem> visible = await reader.Items.ToListAsync();

        Assert.Single(visible);
        Assert.Equal("A-item", visible[0].Name);
        Assert.Equal(CompanyA, visible[0].CompanyId);
    }

    [Fact]
    public async Task Insert_stamps_company_id_from_the_active_tenant()
    {
        string dbName = Guid.NewGuid().ToString();

        await using var context = CreateContext(dbName, CompanyA);
        var item = TenantItem.New(Guid.NewGuid(), "auto-stamped");
        // company_id intentionally left empty — the interceptor must fill it.
        Assert.Equal(Guid.Empty, item.CompanyId);

        context.Items.Add(item);
        await context.SaveChangesAsync();

        Assert.Equal(CompanyA, item.CompanyId);
    }

    [Fact]
    public async Task Insert_with_a_foreign_company_is_blocked()
    {
        string dbName = Guid.NewGuid().ToString();

        await using var context = CreateContext(dbName, CompanyA);
        var item = TenantItem.New(Guid.NewGuid(), "smuggled", ownedBy: CompanyB);
        context.Items.Add(item);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());
        Assert.Contains("Cross-tenant write blocked", ex.Message);
    }

    [Fact]
    public async Task Insert_without_active_tenant_and_without_bypass_fails_fast()
    {
        string dbName = Guid.NewGuid().ToString();

        await using var context = CreateContext(dbName, Guid.Empty);
        context.Items.Add(TenantItem.New(Guid.NewGuid(), "orphan"));

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());
        Assert.Contains("without an active company", ex.Message);
    }

    [Fact]
    public async Task Bypass_scope_disables_the_filter_and_sees_all_companies()
    {
        string dbName = Guid.NewGuid().ToString();

        await using (var contextA = CreateContext(dbName, CompanyA))
        {
            contextA.Items.Add(TenantItem.New(Guid.NewGuid(), "A-item"));
            await contextA.SaveChangesAsync();
        }
        await using (var contextB = CreateContext(dbName, CompanyB))
        {
            contextB.Items.Add(TenantItem.New(Guid.NewGuid(), "B-item"));
            await contextB.SaveChangesAsync();
        }

        var bypass = new TenantBypass(NullLogger<TenantBypass>.Instance);
        await using var context = CreateContext(dbName, CompanyA, bypass);

        // Without the scope: only Company A.
        Assert.Single(await context.Items.ToListAsync());

        using (bypass.BeginScope("test-scan"))
        {
            List<TenantItem> all = await context.Items.ToListAsync();
            Assert.Equal(2, all.Count);
        }

        // Isolation restored after the scope closes.
        Assert.Single(await context.Items.ToListAsync());
    }

    [Fact]
    public void Bypass_scope_requires_an_audit_reason()
    {
        var bypass = new TenantBypass(NullLogger<TenantBypass>.Instance);
        Assert.Throws<ArgumentException>(() => bypass.BeginScope(" "));
    }

    private static TenantTestDbContext CreateContext(
        string databaseName, Guid activeCompanyId, ITenantBypass? bypass = null)
    {
        DbContextOptions<TenantTestDbContext> options =
            new DbContextOptionsBuilder<TenantTestDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

        var tenant = new StubTenantContext(activeCompanyId);
        return new TenantTestDbContext(options, tenant, bypass ?? new TenantBypass(NullLogger<TenantBypass>.Instance));
    }
}

/// <summary>Test-only tenant-scoped entity exercising <see cref="ITenantEntity"/>.</summary>
public sealed class TenantItem : Entity<Guid>, ITenantEntity
{
    // EF Core materialization constructor (parameters bound by property name).
    private TenantItem(Guid id, string name) : base(id) => Name = name;

    public string Name { get; private set; }

    // Private setter is what the interceptor writes through (via EF's change tracker).
    public Guid CompanyId { get; private set; }

    public static TenantItem New(Guid id, string name, Guid ownedBy = default)
        => new(id, name) { CompanyId = ownedBy };
}

/// <summary>Minimal DbContext inheriting the SISLAB tenancy machinery.</summary>
public sealed class TenantTestDbContext : SislabDbContextBase
{
    public TenantTestDbContext(
        DbContextOptions<TenantTestDbContext> options,
        ITenantContext tenantContext,
        ITenantBypass tenantBypass)
        : base(options, tenantContext, tenantBypass) { }

    public DbSet<TenantItem> Items => Set<TenantItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantItem>().HasKey(x => x.Id);
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Immutable tenant context for tests (mirrors a resolved request scope).</summary>
public sealed class StubTenantContext : ITenantContext
{
    public StubTenantContext(Guid companyId) => CompanyId = companyId;

    public Guid CompanyId { get; }
}
