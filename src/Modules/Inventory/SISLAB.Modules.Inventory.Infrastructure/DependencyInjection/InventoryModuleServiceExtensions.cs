using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.Messaging;
using SISLAB.Infrastructure.Messaging.Behaviors;
using SISLAB.Infrastructure.Outbox;
using SISLAB.Infrastructure.Persistence;
using SISLAB.Modules.Inventory.Contracts.Events;
using SISLAB.Modules.Inventory.Domain.Equipments;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.StockItems.Events;
using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.Modules.Inventory.Infrastructure.Messaging;
using SISLAB.Modules.Inventory.Infrastructure.Persistence;
using SISLAB.Modules.Inventory.Infrastructure.ReadModels;
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
        services.AddScoped<IPartnerRepository, PartnerRepository>();
        services.AddScoped<IEquipmentRepository, EquipmentRepository>();

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

        //    TransactionBehavior is registered HERE (not in AddSislabInfrastructure) because it
        //    depends on THIS module's IUnitOfWork. Registered after the shared Logging/Validation
        //    behaviors, so the mediator resolves the pipeline as
        //    Logging → Validation → Transaction → Handler (first registered = outermost).
        //    On commands it calls SaveChangesAsync after the handler; on queries it is a no-op.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        // 4. DomainEvent → IntegrationEvent translators (card [E3] #26). The DomainEventDispatcher
        //    resolves these by DomainEvent type during SaveChanges and enqueues the flattened public
        //    contract into the Outbox, in the aggregate's transaction. Events with no translator are
        //    module-internal and stay off the Outbox. ItemExpiring has no translator here: it is a
        //    derived, time-based signal published by the E6 job, not by the aggregate.
        services.AddScoped<IDomainEventToIntegrationEventTranslator<StockReceivedEvent>, StockReceivedEventTranslator>();
        services.AddScoped<IDomainEventToIntegrationEventTranslator<StockConsumedEvent>, StockConsumedEventTranslator>();
        services.AddScoped<IDomainEventToIntegrationEventTranslator<StockBelowMinimumEvent>, StockBelowMinimumEventTranslator>();

        // 5. Read-model projection (card [E4] #33). The single StockMovementProjectionHandler consumes
        //    the movement integration events published from the Outbox (post-commit, via IEventBus) and
        //    writes one idempotent row per movement into inventory.stock_movements. Registered against
        //    each closed IIntegrationEventHandler<T> so the InMemoryEventBus resolves it per event type.
        //    It writes via IStockMovementStore (Dapper/DbConnectionFactory, ON CONFLICT DO NOTHING for
        //    idempotency), on the read-model side — not through the write DbContext.
        services.AddScoped<IStockMovementStore, StockMovementStore>();
        services.AddScoped<IIntegrationEventHandler<StockReceivedIntegrationEvent>, StockMovementProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler<StockConsumedIntegrationEvent>, StockMovementProjectionHandler>();

        // 6. Applies schema "inventory" migrations at startup (mirrors the Identity pattern).
        services.AddHostedService<InventorySchemaMigrationsHostedService>();

        return services;
    }
}
