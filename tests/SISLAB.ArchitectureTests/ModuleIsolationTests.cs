using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;

namespace SISLAB.ArchitectureTests;

/// <summary>
/// Testes de arquitetura que validam o isolamento entre módulos.
///
/// Regras em vigência:
/// (a) Domain de um módulo NÃO referencia Domain de outro módulo.
/// (b) Comunicação inter-módulo apenas via *.Contracts.
/// (c) Domain não referencia EF Core, ASP.NET ou Dapper.
/// (d) Application depende de Domain, não o contrário.
/// (e) Host não referencia Domain interno dos módulos.
/// (f) SharedKernel não depende de infraestrutura.
///
/// WithoutRequiringPositiveResults() é usado porque os projetos stub ainda não possuem
/// tipos de domínio próprios — zero tipos avaliados é correto neste estágio (E0).
/// Quando tipos forem adicionados nos épicos E1–E4, as regras passarão a ter
/// avaliação positiva obrigatória — remova o WithoutRequiringPositiveResults() conforme
/// cada módulo ganhar seu primeiro tipo de domínio.
/// </summary>
public sealed class ModuleIsolationTests
{
    private static readonly Architecture Architecture = AssemblyRegistry.Architecture;

    // Nomes de assembly (exatamente como declarados no AssemblyName do projeto)
    private const string IdentityDomainAssembly = "SISLAB.Modules.Identity.Domain";
    private const string InventoryDomainAssembly = "SISLAB.Modules.Inventory.Domain";
    private const string SharedKernelAssembly = "SISLAB.SharedKernel";
    private const string InfrastructureAssembly = "SISLAB.Infrastructure";

    /// <summary>
    /// (a+b) Domain do Identity não deve depender de nada do módulo Inventory.
    /// Cross-module Domain reference quebraria a regra de isolamento por bounded context.
    /// </summary>
    [Fact]
    public void IdentityDomain_ShouldNotDependOn_InventoryDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(IdentityDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(InventoryDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (a+b) Domain do Inventory não deve depender de nada do módulo Identity.
    /// </summary>
    [Fact]
    public void InventoryDomain_ShouldNotDependOn_IdentityDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(InventoryDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(IdentityDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (c) Domain do Identity não deve referenciar SISLAB.Infrastructure.
    /// Garante que a camada de domínio não vaze para dependências de infraestrutura.
    /// </summary>
    [Fact]
    public void IdentityDomain_ShouldNotDependOn_Infrastructure()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(IdentityDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(InfrastructureAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (c) Domain do Inventory não deve referenciar SISLAB.Infrastructure.
    /// </summary>
    [Fact]
    public void InventoryDomain_ShouldNotDependOn_Infrastructure()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(InventoryDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(InfrastructureAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (f) SharedKernel não deve depender de nenhum assembly de infraestrutura do SISLAB.
    /// SharedKernel é puro — apenas abstrações e primitivos de domínio.
    /// </summary>
    [Fact]
    public void SharedKernel_ShouldNotDependOn_Infrastructure()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(SharedKernelAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(InfrastructureAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (f) SharedKernel não deve depender de Domain do módulo Identity.
    /// </summary>
    [Fact]
    public void SharedKernel_ShouldNotDependOn_IdentityDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(SharedKernelAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(IdentityDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (f) SharedKernel não deve depender de Domain do módulo Inventory.
    /// </summary>
    [Fact]
    public void SharedKernel_ShouldNotDependOn_InventoryDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(SharedKernelAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(InventoryDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (c) Domain do Identity não deve referenciar EF Core.
    /// </summary>
    [Fact]
    public void IdentityDomain_ShouldNotDependOn_EntityFramework()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(IdentityDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.EntityFrameworkCore")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (c) Domain do Inventory não deve referenciar EF Core.
    /// </summary>
    [Fact]
    public void InventoryDomain_ShouldNotDependOn_EntityFramework()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(InventoryDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.EntityFrameworkCore")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (c) Domain do Identity não deve usar Dapper.
    /// </summary>
    [Fact]
    public void IdentityDomain_ShouldNotDependOn_Dapper()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(IdentityDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Dapper")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (c) Domain do Inventory não deve usar Dapper.
    /// </summary>
    [Fact]
    public void InventoryDomain_ShouldNotDependOn_Dapper()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(InventoryDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Dapper")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (c) Domain do Identity não deve depender de ASP.NET Core.
    /// </summary>
    [Fact]
    public void IdentityDomain_ShouldNotDependOn_AspNetCore()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(IdentityDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.AspNetCore")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (c) Domain do Inventory não deve depender de ASP.NET Core.
    /// </summary>
    [Fact]
    public void InventoryDomain_ShouldNotDependOn_AspNetCore()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(InventoryDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.AspNetCore")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }
}
