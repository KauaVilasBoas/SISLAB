using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.Messaging.Behaviors;
using SISLAB.Infrastructure.Persistence;
using SISLAB.Modules.Configuration.Domain.ExperimentalModels;
using SISLAB.Modules.Configuration.Domain.ExpiryPolicies;
using SISLAB.Modules.Configuration.Domain.ItemCategories;
using SISLAB.Modules.Configuration.Domain.ReferenceRanges;
using SISLAB.Modules.Configuration.Domain.Rooms;
using SISLAB.Modules.Configuration.Domain.Units;
using SISLAB.Modules.Configuration.Infrastructure.Messaging;
using SISLAB.Modules.Configuration.Infrastructure.Persistence;
using SISLAB.Modules.Configuration.Infrastructure.Provisioning;
using SISLAB.Modules.Configuration.Infrastructure.Repositories;
using SISLAB.Modules.Identity.Contracts.Events;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Infrastructure.DependencyInjection;

/// <summary>
/// DI composition for the Configuration module write-side (card [E12] #76).
///
/// Registration order:
/// 1. EF DbContext (config aggregates, schema "configuration").
/// 2. Domain repositories (interface in Domain, implementation here).
/// 3. Write-side unit of work: no-op domain-event dispatcher (this module raises no integration events)
///    and the EF unit of work, so the mediator's TransactionBehavior can commit the config commands.
/// 4. Per-tenant defaults provisioner (idempotent seeder of ExpiryPolicy/categories/units).
/// 5. Configuration schema migrations hosted service.
/// </summary>
public static class ConfigurationModuleServiceExtensions
{
    public static IServiceCollection AddConfigurationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("SislabDb")
            ?? throw new InvalidOperationException(
                "Connection string 'SislabDb' is not configured. " +
                "Set it in appsettings.json or User Secrets.");

        // 1. EF DbContext for the module (schema "configuration"). Tenant services (ITenantContext /
        //    ITenantBypass) are resolved into the constructor, so the base applies the company_id query
        //    filter and the tenant-stamping interceptor at runtime.
        services.AddDbContext<ConfigurationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", schema: "configuration");
                npgsql.MigrationsAssembly(
                    typeof(ConfigurationModuleServiceExtensions).Assembly.GetName().Name);
            }));

        // 2. Domain repositories (one per aggregate).
        services.AddScoped<IExpiryPolicyRepository, ExpiryPolicyRepository>();
        services.AddScoped<IItemCategoryRepository, ItemCategoryRepository>();
        services.AddScoped<IUnitRepository, UnitRepository>();
        services.AddScoped<IReferenceRangeRepository, ReferenceRangeRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IExperimentalModelRepository, ExperimentalModelRepository>();

        // 3. Write-side unit of work for this module's commands. This module raises no integration events,
        //    so the dispatcher is a no-op that just drains any aggregate domain events; IUnitOfWork is the
        //    shared EfUnitOfWork bound to THIS module's DbContext. TransactionBehavior (registered per
        //    module, like Inventory/Notifications) calls SaveChangesAsync after each command.
        services.AddScoped<IDomainEventDispatcher, NoOpConfigurationDomainEventDispatcher>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork<ConfigurationDbContext>>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        // 4. Per-tenant defaults provisioner: seeds ExpiryPolicy(30) + base categories/units for a company,
        //    idempotently and deterministically. Reused by the dev/onboarding seed (card #75 wires the trigger).
        services.AddScoped<TenantConfigurationProvisioner>();

        // 4.1 Cross-module reaction (card [E12] #75b): provision a new tenant's baseline configuration when the
        //      Identity module signals CompanyCreatedIntegrationEvent. The Identity Outbox writes the event on
        //      signup; the background Outbox dispatcher publishes it via IEventBus AFTER commit, which resolves
        //      this handler in-process. It is eventual (off the signup transaction) and idempotent, so an Outbox
        //      retry after a failure re-runs it safely. Registered against the closed IIntegrationEventHandler<T>
        //      so the InMemoryEventBus resolves it by event type.
        services.AddScoped<IIntegrationEventHandler<CompanyCreatedIntegrationEvent>,
            ProvisionTenantOnCompanyCreatedHandler>();

        // 5. Applies schema "configuration" migrations at startup (mirrors the other modules).
        services.AddHostedService<ConfigurationSchemaMigrationsHostedService>();

        return services;
    }
}
