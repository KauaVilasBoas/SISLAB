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
            // Jobs host library (E6 #39): loaded so the Host-style isolation rules can be evaluated
            // against real types — it must depend only on shared Infrastructure/module Application,
            // never on a module's internal Domain.
            typeof(Jobs.Scheduling.TimedBackgroundService).Assembly                 // SISLAB.Jobs
        )
        .Build();

    /// <summary>Arquitetura completa da solução, usada por todos os testes.</summary>
    public static Architecture Architecture => _architecture;
}
