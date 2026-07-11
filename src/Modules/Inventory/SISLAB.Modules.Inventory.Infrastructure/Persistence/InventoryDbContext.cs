using Microsoft.EntityFrameworkCore;
using SISLAB.Infrastructure.Multitenancy;
using SISLAB.Infrastructure.Outbox;
using SISLAB.Infrastructure.Persistence;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.Modules.Inventory.Infrastructure.Persistence.Configurations;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Infrastructure.Persistence;

/// <summary>
/// DbContext for the Inventory module (write-side). Manages the <see cref="StockItem"/> and
/// <see cref="StorageLocation"/> aggregates in the <c>inventory</c> schema.
/// </summary>
/// <remarks>
/// <para>
/// Both aggregates are <see cref="ITenantEntity"/>, so this context is built with the tenant services
/// (<see cref="ITenantContext"/> / <see cref="ITenantBypass"/>) resolved from DI. The base
/// (<see cref="SislabDbContextBase"/>) then applies the global query filter by <c>company_id</c> and
/// installs the tenant-stamping save interceptor. At design time (migrations) the tenant services are
/// absent and the filter is skipped — safe, and correct for schema generation.
/// </para>
/// <para>
/// It participates in the Transactional Outbox pattern (<see cref="IOutboxDbContext"/>): the module
/// raises domain events (StockReceivedEvent, StockConsumedEvent, ...) that <see cref="EfUnitOfWork{TContext}"/>
/// translates and writes to <c>outbox_messages</c> in the same transaction as the aggregate change.
/// </para>
/// </remarks>
public sealed class InventoryDbContext : SislabDbContextBase, IOutboxDbContext
{
    public InventoryDbContext(
        DbContextOptions<InventoryDbContext> options,
        ITenantContext? tenantContext = null,
        ITenantBypass? tenantBypass = null)
        : base(options, tenantContext, tenantBypass) { }

    public DbSet<StockItem> StockItems => Set<StockItem>();

    public DbSet<StorageLocation> StorageLocations => Set<StorageLocation>();

    /// <inheritdoc />
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Every table of this module (aggregates + outbox) lives in the "inventory" schema.
        // Setting it as the default schema keeps the configurations free of per-table schema noise
        // and, crucially, places the shared OutboxMessageConfiguration's table in "inventory" too.
        modelBuilder.HasDefaultSchema("inventory");

        modelBuilder.ApplyConfiguration(new StockItemConfiguration());
        modelBuilder.ApplyConfiguration(new StorageLocationConfiguration());

        // Outbox table lives in the module schema so the aggregate write and the outbox write
        // share one transaction/one connection (local transactional consistency).
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        // snake_case naming + tenant query filter applied by the base AFTER the configurations,
        // so it can see every mapped entity type (including OutboxMessage).
        base.OnModelCreating(modelBuilder);
    }
}
