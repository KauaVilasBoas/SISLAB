using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace SISLAB.ArchitectureTests;

/// <summary>
/// Architecture rules that keep MVC controllers thin and CQRS-only.
///
/// Controllers are the HTTP boundary: they dispatch via <c>IMediator</c> and map the
/// successful result to <c>ApiResult</c>. They must NOT reach into persistence — no
/// repositories, no DbContext, no Dapper data access. Read/write logic lives behind
/// the mediator in query/command handlers.
///
/// Controllers are identified by the conventional <c>Controller</c> name suffix (they also
/// carry <c>[ApiController]</c> via <c>SislabControllerBase</c>). The tenant-aware base class
/// <c>SislabControllerBase</c> itself lives in shared Infrastructure by design, so the rules
/// below target persistence types specifically rather than the whole Infrastructure assembly.
/// </summary>
public sealed class ControllerDependencyTests
{
    private static readonly Architecture Architecture = AssemblyRegistry.Architecture;

    /// <summary>
    /// Controllers must not depend on any repository. Persistence access belongs to
    /// query/command handlers reached through the mediator, never the HTTP boundary.
    /// </summary>
    [Fact]
    public void Controllers_ShouldNotDependOn_Repositories()
    {
        IArchRule rule = Types().That()
            .HaveNameEndingWith("Controller")
            .Should().NotDependOnAnyTypesThat().HaveNameContaining("Repository")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// Controllers must not depend on EF Core (DbContext) directly — write-side persistence
    /// is owned by aggregates and handlers, not the controller.
    /// </summary>
    [Fact]
    public void Controllers_ShouldNotDependOn_EntityFramework()
    {
        IArchRule rule = Types().That()
            .HaveNameEndingWith("Controller")
            .Should().NotDependOnAnyTypesThat().ResideInNamespace("Microsoft.EntityFrameworkCore")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// Controllers must not depend on Dapper — read-side data access is owned by query handlers.
    /// </summary>
    [Fact]
    public void Controllers_ShouldNotDependOn_Dapper()
    {
        IArchRule rule = Types().That()
            .HaveNameEndingWith("Controller")
            .Should().NotDependOnAnyTypesThat().ResideInNamespace("Dapper")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    /// <summary>
    /// Controllers must not depend on a module's Infrastructure (DbContext, concrete repositories,
    /// Dapper data access). Their only injected collaborator is <c>IMediator</c>; tenant access
    /// comes exclusively through <c>SislabControllerBase</c> in shared Infrastructure, which is why
    /// this rule targets the per-module Infrastructure assemblies rather than shared Infrastructure.
    /// </summary>
    [Fact]
    public void Controllers_ShouldNotDependOn_ModuleInfrastructure()
    {
        IArchRule rule = Types().That()
            .HaveNameEndingWith("Controller")
            .Should().NotDependOnAnyTypesThat().ResideInNamespace("SISLAB.Modules.Identity.Infrastructure")
            .AndShould().NotDependOnAnyTypesThat().ResideInNamespace("SISLAB.Modules.Inventory.Infrastructure")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }
}
