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
    private const string InventoryApplicationAssembly = "SISLAB.Modules.Inventory.Application";
    private const string InventoryInfrastructureAssembly = "SISLAB.Modules.Inventory.Infrastructure";
    private const string InventoryContractsAssembly = "SISLAB.Modules.Inventory.Contracts";
    private const string NotificationsDomainAssembly = "SISLAB.Modules.Notifications.Domain";
    private const string NotificationsApplicationAssembly = "SISLAB.Modules.Notifications.Application";
    private const string NotificationsInfrastructureAssembly = "SISLAB.Modules.Notifications.Infrastructure";
    private const string NotificationsContractsAssembly = "SISLAB.Modules.Notifications.Contracts";
    private const string AuditContractsAssembly = "SISLAB.Modules.Audit.Contracts";
    private const string AuditApplicationAssembly = "SISLAB.Modules.Audit.Application";
    private const string AuditInfrastructureAssembly = "SISLAB.Modules.Audit.Infrastructure";
    private const string ConfigurationDomainAssembly = "SISLAB.Modules.Configuration.Domain";
    private const string ConfigurationApplicationAssembly = "SISLAB.Modules.Configuration.Application";
    private const string ConfigurationContractsAssembly = "SISLAB.Modules.Configuration.Contracts";
    private const string ConfigurationInfrastructureAssembly = "SISLAB.Modules.Configuration.Infrastructure";
    private const string ExperimentsDomainAssembly = "SISLAB.Modules.Experiments.Domain";
    private const string ExperimentsApplicationAssembly = "SISLAB.Modules.Experiments.Application";
    private const string ExperimentsContractsAssembly = "SISLAB.Modules.Experiments.Contracts";
    private const string ExperimentsInfrastructureAssembly = "SISLAB.Modules.Experiments.Infrastructure";
    private const string SharedKernelAssembly = "SISLAB.SharedKernel";
    private const string InfrastructureAssembly = "SISLAB.Infrastructure";
    private const string JobsAssembly = "SISLAB.Jobs";

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
    /// (f) SharedKernel deve permanecer PURO também para a nova abstração de multi-tenancy do E6
    /// (<c>ITenantContextOverride</c>, o seam de override usado pelos jobs de alerta #41/#42/#66):
    /// nenhum tipo do SharedKernel pode referenciar EF Core. O seam settável vive no SharedKernel como
    /// contrato puro; suas implementações (<c>TenantContextOverride</c>, <c>OverridableTenantContext</c>)
    /// vivem na Infrastructure. Esta regra garante que a abstração nova não arrasta persistência para o Shared.
    /// </summary>
    [Fact]
    public void SharedKernel_ShouldNotDependOn_EntityFramework()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(SharedKernelAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.EntityFrameworkCore")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (f) SharedKernel não deve depender de ASP.NET Core — reforça a pureza do Shared, incluindo a nova
    /// abstração de override de tenant do E6, que não pode conhecer o pipeline HTTP.
    /// </summary>
    [Fact]
    public void SharedKernel_ShouldNotDependOn_AspNetCore()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(SharedKernelAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.AspNetCore")
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

    // ---------------------------------------------------------------------------------------------
    // (e) Jobs host library (card [E6] #39). SISLAB.Jobs runs in-process with the API and follows the
    //     same isolation rules as the Host: it may use shared Infrastructure and each module's public
    //     entry point (Application), but must NEVER reach into a module's internal Domain. Guarding
    //     this keeps the background worker from coupling to aggregates/value objects behind a module's
    //     boundary — it talks to modules only through their public surface (mediator/Contracts).
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// (e) O host de Jobs não deve depender do Domain interno do módulo Identity.
    /// </summary>
    [Fact]
    public void Jobs_ShouldNotDependOn_IdentityDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(JobsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(IdentityDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (e) O host de Jobs não deve depender do Domain interno do módulo Inventory.
    /// </summary>
    [Fact]
    public void Jobs_ShouldNotDependOn_InventoryDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(JobsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(InventoryDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    // ---------------------------------------------------------------------------------------------
    // (b) Public boundary of the Inventory module (card [E5] #35).
    //
    // Other modules may depend ONLY on SISLAB.Modules.Inventory.Contracts (IInventoryApi + DTOs),
    // never on the module's Domain/Application/Infrastructure internals. Since there is no external
    // consumer yet, we enforce the necessary condition that makes such a consumer safe: the Contracts
    // assembly itself must not reach into the module's internals — so referencing it never transitively
    // drags in Domain/Application/Infrastructure. If Contracts stays clean, an external module that
    // references only Contracts cannot touch the internals through it.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// (b) O boundary público (Contracts) do Inventory não deve depender do Domain interno do módulo.
    /// </summary>
    [Fact]
    public void InventoryContracts_ShouldNotDependOn_InventoryDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(InventoryContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(InventoryDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (b) O boundary público (Contracts) do Inventory não deve depender da Application do módulo.
    /// </summary>
    [Fact]
    public void InventoryContracts_ShouldNotDependOn_InventoryApplication()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(InventoryContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(InventoryApplicationAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (b) O boundary público (Contracts) do Inventory não deve depender da Infrastructure do módulo.
    /// </summary>
    [Fact]
    public void InventoryContracts_ShouldNotDependOn_InventoryInfrastructure()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(InventoryContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(InventoryInfrastructureAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (b) O boundary público (Contracts) do Inventory não deve depender de EF Core — DTOs são
    /// primitivos, sem tipos de persistência vazando pela fronteira pública.
    /// </summary>
    [Fact]
    public void InventoryContracts_ShouldNotDependOn_EntityFramework()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(InventoryContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.EntityFrameworkCore")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    // ---------------------------------------------------------------------------------------------
    // Notifications module (card #64a). New bounded context that the E6 jobs raise alerts into. It
    // follows the same isolation rules as the other modules:
    // (c) its Domain never touches EF Core, Dapper or ASP.NET;
    // (a+b) its Domain never depends on another module's Domain;
    // (b) its public Contracts boundary stays clean (no Domain/Application/Infrastructure/EF), so a
    //     consumer that references only Contracts (the Jobs host) cannot reach the internals through it;
    // (e) the Jobs host never depends on the module's internal Domain — it talks to it only via Contracts.
    // ---------------------------------------------------------------------------------------------

    /// <summary>(c) Domain do Notifications não deve referenciar SISLAB.Infrastructure.</summary>
    [Fact]
    public void NotificationsDomain_ShouldNotDependOn_Infrastructure()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(NotificationsDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(InfrastructureAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(c) Domain do Notifications não deve referenciar EF Core.</summary>
    [Fact]
    public void NotificationsDomain_ShouldNotDependOn_EntityFramework()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(NotificationsDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.EntityFrameworkCore")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(c) Domain do Notifications não deve usar Dapper.</summary>
    [Fact]
    public void NotificationsDomain_ShouldNotDependOn_Dapper()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(NotificationsDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Dapper")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(c) Domain do Notifications não deve depender de ASP.NET Core.</summary>
    [Fact]
    public void NotificationsDomain_ShouldNotDependOn_AspNetCore()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(NotificationsDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.AspNetCore")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(a+b) Domain do Notifications não deve depender do Domain do Inventory.</summary>
    [Fact]
    public void NotificationsDomain_ShouldNotDependOn_InventoryDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(NotificationsDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(InventoryDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(a+b) Domain do Notifications não deve depender do Domain do Identity.</summary>
    [Fact]
    public void NotificationsDomain_ShouldNotDependOn_IdentityDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(NotificationsDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(IdentityDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(b) O boundary público (Contracts) do Notifications não deve depender do seu Domain interno.</summary>
    [Fact]
    public void NotificationsContracts_ShouldNotDependOn_NotificationsDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(NotificationsContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(NotificationsDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(b) O boundary público (Contracts) do Notifications não deve depender da Application do módulo.</summary>
    [Fact]
    public void NotificationsContracts_ShouldNotDependOn_NotificationsApplication()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(NotificationsContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(NotificationsApplicationAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(b) O boundary público (Contracts) do Notifications não deve depender da Infrastructure do módulo.</summary>
    [Fact]
    public void NotificationsContracts_ShouldNotDependOn_NotificationsInfrastructure()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(NotificationsContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(NotificationsInfrastructureAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(b) O boundary público (Contracts) do Notifications não deve depender de EF Core.</summary>
    [Fact]
    public void NotificationsContracts_ShouldNotDependOn_EntityFramework()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(NotificationsContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.EntityFrameworkCore")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// (e) O host de Jobs pode referenciar Notifications.Contracts (INotificationPublisher), mas NÃO deve
    /// depender do Domain interno do módulo Notifications.
    /// </summary>
    [Fact]
    public void Jobs_ShouldNotDependOn_NotificationsDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(JobsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(NotificationsDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    // ---------------------------------------------------------------------------------------------
    // Audit module (card [E9] #57). The append-only compliance trail has no Domain project (it is
    // Dapper-only, write-once), so the rules focus on keeping its public Contracts boundary clean —
    // the boundary that other modules (Inventory) reference to record entries — and on ensuring the
    // trail never reaches into a business module's internal Domain.
    // (b) Contracts must not depend on the module's Application/Infrastructure, nor on EF Core, so a
    //     consumer that references only Audit.Contracts (Inventory) cannot reach the internals through it.
    // (a) The Audit module must not depend on any business module's Domain (Inventory/Identity).
    // ---------------------------------------------------------------------------------------------

    /// <summary>(b) O boundary público (Contracts) do Audit não deve depender da Application do módulo.</summary>
    [Fact]
    public void AuditContracts_ShouldNotDependOn_AuditApplication()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(AuditContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(AuditApplicationAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(b) O boundary público (Contracts) do Audit não deve depender da Infrastructure do módulo.</summary>
    [Fact]
    public void AuditContracts_ShouldNotDependOn_AuditInfrastructure()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(AuditContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(AuditInfrastructureAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(b) O boundary público (Contracts) do Audit não deve depender de EF Core.</summary>
    [Fact]
    public void AuditContracts_ShouldNotDependOn_EntityFramework()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(AuditContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.EntityFrameworkCore")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(a) O módulo Audit (Infrastructure) não deve depender do Domain interno do Inventory.</summary>
    [Fact]
    public void AuditInfrastructure_ShouldNotDependOn_InventoryDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(AuditInfrastructureAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(InventoryDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(a) O módulo Audit (Application) não deve depender do Domain interno do Inventory.</summary>
    [Fact]
    public void AuditApplication_ShouldNotDependOn_InventoryDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(AuditApplicationAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(InventoryDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    // ---------------------------------------------------------------------------------------------
    // Configuration module (card [E12] #76). The transversal per-tenant settings source that other
    // business modules read from through the public ILabConfiguration port.
    // (b) Its public Contracts boundary — the surface Inventory references — must not leak the module's
    //     Application/Infrastructure or EF Core.
    // (a) The Inventory module, which consumes Configuration only via Contracts, must never reach into
    //     the Configuration internal Domain/Application/Infrastructure (module isolation, section 2).
    // ---------------------------------------------------------------------------------------------

    /// <summary>(b) O boundary público (Contracts) da Configuration não deve depender da Application do módulo.</summary>
    [Fact]
    public void ConfigurationContracts_ShouldNotDependOn_ConfigurationApplication()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(ConfigurationContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(ConfigurationApplicationAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(b) O boundary público (Contracts) da Configuration não deve depender da Infrastructure do módulo.</summary>
    [Fact]
    public void ConfigurationContracts_ShouldNotDependOn_ConfigurationInfrastructure()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(ConfigurationContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(ConfigurationInfrastructureAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(b) O boundary público (Contracts) da Configuration não deve depender do Domain interno do módulo.</summary>
    [Fact]
    public void ConfigurationContracts_ShouldNotDependOn_ConfigurationDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(ConfigurationContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(ConfigurationDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(b) O boundary público (Contracts) da Configuration não deve depender de EF Core.</summary>
    [Fact]
    public void ConfigurationContracts_ShouldNotDependOn_EntityFramework()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(ConfigurationContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.EntityFrameworkCore")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(a) O Inventory (Application) só consome a Configuration via Contracts, nunca seu Domain interno.</summary>
    [Fact]
    public void InventoryApplication_ShouldNotDependOn_ConfigurationDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(InventoryApplicationAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(ConfigurationDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(a) O Inventory (Application) não deve depender da Application interna da Configuration.</summary>
    [Fact]
    public void InventoryApplication_ShouldNotDependOn_ConfigurationApplication()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(InventoryApplicationAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(ConfigurationApplicationAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(a) O Inventory (Application) não deve depender da Infrastructure interna da Configuration.</summary>
    [Fact]
    public void InventoryApplication_ShouldNotDependOn_ConfigurationInfrastructure()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(InventoryApplicationAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(ConfigurationInfrastructureAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    // ---------------------------------------------------------------------------------------------
    // (a) Experiments consumes Configuration only via Contracts (SISLAB-04). The Experiments write-side binds a
    //     batch to an experimental model owned by the Configuration bounded context, validating it through the
    //     public ILabConfiguration port and keeping the model id by value. It must therefore NEVER reach into
    //     Configuration's internal Domain/Application/Infrastructure — the boundary is the Contracts assembly
    //     only (module isolation, section 2), symmetric to the Inventory → Configuration rules above.
    // ---------------------------------------------------------------------------------------------

    /// <summary>(a) O Experiments (Application) só consome a Configuration via Contracts, nunca seu Domain interno.</summary>
    [Fact]
    public void ExperimentsApplication_ShouldNotDependOn_ConfigurationDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(ExperimentsApplicationAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(ConfigurationDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(a) O Experiments (Application) não deve depender da Application interna da Configuration.</summary>
    [Fact]
    public void ExperimentsApplication_ShouldNotDependOn_ConfigurationApplication()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(ExperimentsApplicationAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(ConfigurationApplicationAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(a) O Experiments (Application) não deve depender da Infrastructure interna da Configuration.</summary>
    [Fact]
    public void ExperimentsApplication_ShouldNotDependOn_ConfigurationInfrastructure()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(ExperimentsApplicationAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(ConfigurationInfrastructureAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    // ---------------------------------------------------------------------------------------------
    // Experiments module (card [E11] #68). New bounded context (the in vitro viability slice). Same isolation
    // rules as the other modules:
    // (c) its Domain never touches EF Core, Dapper or ASP.NET;
    // (a+b) its Domain never depends on another module's Domain;
    // (b) its public Contracts boundary stays clean (no Domain/Application/Infrastructure/EF), so a consumer
    //     that references only Contracts (e.g. Inventory correlating cost) cannot reach the internals through it.
    // ---------------------------------------------------------------------------------------------

    /// <summary>(c) Domain do Experiments não deve referenciar SISLAB.Infrastructure.</summary>
    [Fact]
    public void ExperimentsDomain_ShouldNotDependOn_Infrastructure()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(ExperimentsDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(InfrastructureAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(c) Domain do Experiments não deve referenciar EF Core.</summary>
    [Fact]
    public void ExperimentsDomain_ShouldNotDependOn_EntityFramework()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(ExperimentsDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.EntityFrameworkCore")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(c) Domain do Experiments não deve usar Dapper.</summary>
    [Fact]
    public void ExperimentsDomain_ShouldNotDependOn_Dapper()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(ExperimentsDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Dapper")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(c) Domain do Experiments não deve depender de ASP.NET Core.</summary>
    [Fact]
    public void ExperimentsDomain_ShouldNotDependOn_AspNetCore()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(ExperimentsDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.AspNetCore")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(a+b) Domain do Experiments não deve depender do Domain do Inventory.</summary>
    [Fact]
    public void ExperimentsDomain_ShouldNotDependOn_InventoryDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(ExperimentsDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(InventoryDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(a+b) Domain do Experiments não deve depender do Domain do Identity.</summary>
    [Fact]
    public void ExperimentsDomain_ShouldNotDependOn_IdentityDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(ExperimentsDomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(IdentityDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(b) O boundary público (Contracts) do Experiments não deve depender do seu Domain interno.</summary>
    [Fact]
    public void ExperimentsContracts_ShouldNotDependOn_ExperimentsDomain()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(ExperimentsContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(ExperimentsDomainAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(b) O boundary público (Contracts) do Experiments não deve depender da Application do módulo.</summary>
    [Fact]
    public void ExperimentsContracts_ShouldNotDependOn_ExperimentsApplication()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(ExperimentsContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(ExperimentsApplicationAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(b) O boundary público (Contracts) do Experiments não deve depender da Infrastructure do módulo.</summary>
    [Fact]
    public void ExperimentsContracts_ShouldNotDependOn_ExperimentsInfrastructure()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(ExperimentsContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInAssembly(ExperimentsInfrastructureAssembly)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>(b) O boundary público (Contracts) do Experiments não deve depender de EF Core.</summary>
    [Fact]
    public void ExperimentsContracts_ShouldNotDependOn_EntityFramework()
    {
        IArchRule rule = ArchRuleDefinition
            .Types().That().ResideInAssembly(ExperimentsContractsAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.EntityFrameworkCore")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }
}
