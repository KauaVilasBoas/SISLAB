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
            typeof(Modules.Inventory.Application.InventoryModule).Assembly           // Inventory.Application
        )
        .Build();

    /// <summary>Arquitetura completa da solução, usada por todos os testes.</summary>
    public static Architecture Architecture => _architecture;
}
