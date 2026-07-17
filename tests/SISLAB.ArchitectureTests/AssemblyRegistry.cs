using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

namespace SISLAB.ArchitectureTests;

/// <summary>
/// Registro central dos assemblies analisados pelos testes de arquitetura.
/// ArchUnitNET carrega os assemblies uma única vez (lazy + cached) para performance.
/// </summary>
internal static class AssemblyRegistry
{
    private static readonly Architecture _architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(SharedKernel.Domain.Entity<Guid>).Assembly,                       // SISLAB.SharedKernel
            typeof(Infrastructure.Persistence.SislabDbContextBase).Assembly,         // SISLAB.Infrastructure
            typeof(Modules.Identity.Application.IdentityModule).Assembly,            // Identity.Application
            typeof(Modules.Inventory.Application.InventoryModule).Assembly,          // Inventory.Application
            // Loaded explicitly so the module-isolation rules (E5 #35) can evaluate the module's public
            // Contracts boundary and its internal Infrastructure against real types, not zero types.
            typeof(Modules.Inventory.Contracts.IInventoryApi).Assembly,             // Inventory.Contracts
            typeof(Modules.Inventory.Infrastructure.Persistence.InventoryDbContext).Assembly, // Inventory.Infrastructure
            // Notifications module (card #64a): all four projects loaded so the module-isolation rules can
            // evaluate the Domain internals and the public Contracts boundary against real types.
            typeof(Modules.Notifications.Domain.Notifications.Notification).Assembly,      // Notifications.Domain
            typeof(Modules.Notifications.Application.NotificationsModule).Assembly,        // Notifications.Application
            typeof(Modules.Notifications.Contracts.INotificationPublisher).Assembly,       // Notifications.Contracts
            typeof(Modules.Notifications.Infrastructure.Persistence.NotificationsDbContext).Assembly, // Notifications.Infrastructure
            // Audit module (card [E9] #57): the append-only compliance trail. It has no Domain project
            // (Dapper-only, write-once), so only its public Contracts boundary and its Application +
            // Infrastructure are loaded, letting the isolation rules keep the Contracts surface clean and
            // ensure the trail never leaks a business module's internals.
            typeof(Modules.Audit.Contracts.IAuditWriter).Assembly,                  // Audit.Contracts
            typeof(Modules.Audit.Application.AuditModule).Assembly,                  // Audit.Application
            typeof(Modules.Audit.Infrastructure.DependencyInjection.AuditModuleServiceExtensions).Assembly, // Audit.Infrastructure
            // Configuration module (card [E12] #76): all four projects loaded so the module-isolation rules
            // can evaluate the Domain internals and the public Contracts boundary against real types.
            typeof(Modules.Configuration.Domain.Rooms.Room).Assembly,                     // Configuration.Domain
            typeof(Modules.Configuration.Application.ConfigurationModule).Assembly,        // Configuration.Application
            typeof(Modules.Configuration.Contracts.ILabConfiguration).Assembly,            // Configuration.Contracts
            typeof(Modules.Configuration.Infrastructure.Persistence.ConfigurationDbContext).Assembly, // Configuration.Infrastructure
            // Experiments module (card [E11] #68): all four projects loaded so the module-isolation rules can
            // evaluate the Domain internals (the Experiment aggregate + strategy) and the public Contracts
            // boundary (the ExperimentCalculated integration event) against real types.
            typeof(Modules.Experiments.Domain.Experiments.Experiment).Assembly,                          // Experiments.Domain
            typeof(Modules.Experiments.Application.ExperimentsModule).Assembly,                          // Experiments.Application
            typeof(Modules.Experiments.Contracts.Events.ExperimentCalculatedIntegrationEvent).Assembly,  // Experiments.Contracts
            typeof(Modules.Experiments.Infrastructure.Persistence.ExperimentsDbContext).Assembly,        // Experiments.Infrastructure
            // Jobs host library (E6 #39): loaded so the Host-style isolation rules can be evaluated
            // against real types — it must depend only on shared Infrastructure/module Application,
            // never on a module's internal Domain.
            typeof(Jobs.Scheduling.TimedBackgroundService).Assembly                 // SISLAB.Jobs
        )
        .Build();

    /// <summary>Arquitetura completa da solução, usada por todos os testes.</summary>
    public static Architecture Architecture => _architecture;
}
