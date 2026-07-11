using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.Messaging;
using SISLAB.Infrastructure.Outbox;
using SISLAB.Infrastructure.Persistence;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.Modules.Inventory.Infrastructure.Persistence;
using SISLAB.Modules.Inventory.Infrastructure.Repositories;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Infrastructure.DependencyInjection;

/// <summary>
/// DI composition for the Inventory module write-side (card [E3] #25).
///
/// Registration order:
/// 1. EF DbContext (StockItem / StorageLocation / OutboxMessage, schema "inventory").
/// 2. Domain repositories.
/// 3. Write-side unit of work: Outbox writer, domain-event dispatcher and the EF unit of work.
/// 4. Inventory schema migrations hosted service.
/// </summary>
public static class InventoryModuleServiceExtensions
{
    public static IServiceCollection AddInventoryModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("SislabDb")
            ?? throw new InvalidOperationException(
                "Connection string 'SislabDb' is not configured. " +
                "Set it in appsettings.json or User Secrets.");

        // 1. EF DbContext for the module (schema "inventory"). The tenant services (ITenantContext /
        //    ITenantBypass) are resolved by DI into InventoryDbContext's constructor, so the base
        //    applies the company_id query filter and the tenant-stamping interceptor at runtime.
        services.AddDbContext<InventoryDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", schema: "inventory");
                npgsql.MigrationsAssembly(
                    typeof(InventoryModuleServiceExtensions).Assembly.GetName().Name);
            }));

        // 2. Domain repositories (interface in Domain, implementation here).
        services.AddScoped<IStockItemRepository, StockItemRepository>();
        services.AddScoped<IStorageLocationRepository, StorageLocationRepository>();

        // 3. Write-side unit of work (hybrid consistency strategy — E2).
        //    - IOutboxDbContext points at THIS module's DbContext so the Outbox write shares the
        //      aggregate's transaction.
        //    - OutboxWriter serializes integration events into outbox_messages.
        //    - IDomainEventDispatcher runs transactional handlers and enqueues events to the Outbox.
        //    - IUnitOfWork = EfUnitOfWork<InventoryDbContext>: SaveChanges is triggered by the
        //      mediator's TransactionBehavior after each command.
        services.AddScoped<IOutboxDbContext>(sp => sp.GetRequiredService<InventoryDbContext>());
        services.AddScoped<OutboxWriter>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork<InventoryDbContext>>();

        // 4. Applies schema "inventory" migrations at startup (mirrors the Identity pattern).
        services.AddHostedService<InventorySchemaMigrationsHostedService>();

        return services;
    }
}
