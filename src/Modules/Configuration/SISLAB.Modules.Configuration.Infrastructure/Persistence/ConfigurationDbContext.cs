using Microsoft.EntityFrameworkCore;
using SISLAB.Infrastructure.Multitenancy;
using SISLAB.Infrastructure.Persistence;
using SISLAB.Modules.Configuration.Domain.ExpiryPolicies;
using SISLAB.Modules.Configuration.Domain.ItemCategories;
using SISLAB.Modules.Configuration.Domain.ReferenceRanges;
using SISLAB.Modules.Configuration.Domain.Rooms;
using SISLAB.Modules.Configuration.Domain.Units;
using SISLAB.Modules.Configuration.Infrastructure.Persistence.Configurations;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Infrastructure.Persistence;

/// <summary>
/// DbContext for the Configuration module (write-side). Manages the per-tenant configuration aggregates
/// (<see cref="ExpiryPolicy"/>, <see cref="ItemCategory"/>, <see cref="Unit"/>, <see cref="ReferenceRange"/>,
/// <see cref="Room"/>) in the <c>configuration</c> schema.
/// </summary>
/// <remarks>
/// Every aggregate is an <see cref="ITenantEntity"/>, so this context is built with the tenant services
/// (<see cref="ITenantContext"/> / <see cref="ITenantBypass"/>) resolved from DI; the base
/// (<see cref="SislabDbContextBase"/>) then applies the global query filter by <c>company_id</c> and installs
/// the tenant-stamping save interceptor. At design time (migrations) the tenant services are absent and the
/// filter is skipped — correct for schema generation.
/// </remarks>
public sealed class ConfigurationDbContext : SislabDbContextBase
{
    public ConfigurationDbContext(
        DbContextOptions<ConfigurationDbContext> options,
        ITenantContext? tenantContext = null,
        ITenantBypass? tenantBypass = null)
        : base(options, tenantContext, tenantBypass) { }

    public DbSet<ExpiryPolicy> ExpiryPolicies => Set<ExpiryPolicy>();

    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();

    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<ReferenceRange> ReferenceRanges => Set<ReferenceRange>();

    public DbSet<Room> Rooms => Set<Room>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Every table of this module lives in the "configuration" schema.
        modelBuilder.HasDefaultSchema("configuration");

        modelBuilder.ApplyConfiguration(new ExpiryPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new ItemCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new UnitConfiguration());
        modelBuilder.ApplyConfiguration(new ReferenceRangeConfiguration());
        modelBuilder.ApplyConfiguration(new RoomConfiguration());

        // snake_case naming + tenant query filter applied by the base AFTER the configurations.
        base.OnModelCreating(modelBuilder);
    }
}
