using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.Messaging;
using SISLAB.Infrastructure.Messaging.Behaviors;
using SISLAB.Infrastructure.Outbox;
using SISLAB.Infrastructure.Persistence;
using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.Modules.Experiments.Domain.Collection;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Domain.Experiments.Events;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.Modules.Experiments.Infrastructure.Messaging;
using SISLAB.Modules.Experiments.Infrastructure.Persistence;
using SISLAB.Modules.Experiments.Infrastructure.Repositories;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Infrastructure.DependencyInjection;

/// <summary>
/// DI composition for the Experiments module write-side (decision card #68).
///
/// Registration order:
/// 1. EF DbContext (Experiment TPH hierarchy + Outbox, schema "experiments").
/// 2. Domain repository.
/// 3. Write-side unit of work: Outbox writer, domain-event dispatcher and the EF unit of work.
/// 4. DomainEvent → IntegrationEvent translator (ExperimentCalculated → Inventory correlation).
/// 5. Experiments schema migrations hosted service.
/// </summary>
public static class ExperimentsModuleServiceExtensions
{
    public static IServiceCollection AddExperimentsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("SislabDb")
            ?? throw new InvalidOperationException(
                "Connection string 'SislabDb' is not configured. " +
                "Set it in appsettings.json or User Secrets.");

        // 1. EF DbContext for the module (schema "experiments"). Tenant services (ITenantContext /
        //    ITenantBypass) are resolved into the constructor, so the base applies the company_id query filter
        //    and the tenant-stamping interceptor at runtime.
        services.AddDbContext<ExperimentsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", schema: "experiments");
                npgsql.MigrationsAssembly(
                    typeof(ExperimentsModuleServiceExtensions).Assembly.GetName().Name);
            }));

        // 2. Domain repositories (interface in Domain, implementation here).
        services.AddScoped<IExperimentRepository, ExperimentRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ISampleRepository, SampleRepository>();
        services.AddScoped<ICollectionPlanRepository, CollectionPlanRepository>();

        // 3. Write-side unit of work (hybrid consistency strategy — E2).
        //    - IOutboxDbContext points at THIS module's DbContext so the Outbox write shares the aggregate's
        //      transaction.
        //    - OutboxWriter serializes integration events into outbox_messages.
        //    - IDomainEventDispatcher runs transactional handlers and enqueues events to the Outbox.
        //    - IUnitOfWork = EfUnitOfWork<ExperimentsDbContext>: SaveChanges is triggered by the mediator's
        //      TransactionBehavior after each command.
        services.AddScoped<IOutboxDbContext>(sp => sp.GetRequiredService<ExperimentsDbContext>());
        services.AddScoped<OutboxWriter>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork<ExperimentsDbContext>>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        // 4. DomainEvent → IntegrationEvent translator. The DomainEventDispatcher resolves it by domain-event
        //    type during SaveChanges and enqueues the flattened public contract into the Outbox, in the
        //    aggregate's transaction. ExperimentCreatedEvent has no translator: it is module-internal.
        services.AddScoped<IDomainEventToIntegrationEventTranslator<ExperimentCalculatedEvent>,
            ExperimentCalculatedEventTranslator>();

        // 5. Applies schema "experiments" migrations at startup (mirrors the other modules).
        services.AddHostedService<ExperimentsSchemaMigrationsHostedService>();

        return services;
    }
}
