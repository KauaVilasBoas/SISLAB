using SISLAB.Modules.Experiments.Application.Collection.Commands;
using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.Modules.Experiments.Domain.Collection;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.Modules.Experiments.Tests.Fakes;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// Covers the SISLAB-08 collection-plan write handlers: creating a plan validates the batch and the one-per-batch rule;
/// defining a routing validates the storage room across the Configuration boundary; assigning a role validates the role
/// (Configuration) and the member's active membership (Identity). All cross-module checks go through Contracts fakes.
/// </summary>
public sealed class CollectionPlanCommandHandlerTests
{
    private static readonly Guid Company = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static (Project project, Guid batchId) ProjectWithBatch()
    {
        Project project = Project.Create("Neuropatia diabética", "Rattus norvegicus");
        Batch batch = project.AddBatch("Leva 1");
        return (project, batch.Id);
    }

    [Fact]
    public async Task Create_validates_the_batch_and_persists_a_new_plan()
    {
        (Project project, Guid batchId) = ProjectWithBatch();
        FakeProjectRepository projects = new FakeProjectRepository().Seed(project);
        FakeCollectionPlanRepository plans = new();
        CreateCollectionPlanCommandHandler handler = new(plans, projects, new StubTenantContext(Company));

        Guid id = await handler.HandleAsync(new CreateCollectionPlanCommand(project.Id, batchId));

        Assert.NotNull(plans.LastAdded);
        Assert.Equal(id, plans.LastAdded!.Id);
        Assert.Equal(Company, plans.LastAdded.CompanyId);
        Assert.Equal(batchId, plans.LastAdded.BatchId);
    }

    [Fact]
    public async Task Create_rejects_a_batch_not_in_the_project()
    {
        (Project project, _) = ProjectWithBatch();
        FakeProjectRepository projects = new FakeProjectRepository().Seed(project);
        CreateCollectionPlanCommandHandler handler =
            new(new FakeCollectionPlanRepository(), projects, new StubTenantContext(Company));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new CreateCollectionPlanCommand(project.Id, Guid.NewGuid())));
    }

    [Fact]
    public async Task Create_rejects_a_second_plan_for_the_same_batch()
    {
        (Project project, Guid batchId) = ProjectWithBatch();
        FakeProjectRepository projects = new FakeProjectRepository().Seed(project);
        FakeCollectionPlanRepository plans = new FakeCollectionPlanRepository()
            .Seed(CollectionPlan.Create(Company, project.Id, batchId));
        CreateCollectionPlanCommandHandler handler = new(plans, projects, new StubTenantContext(Company));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.HandleAsync(new CreateCollectionPlanCommand(project.Id, batchId)));
    }

    [Fact]
    public async Task DefineRouting_rejects_a_storage_room_not_in_the_company()
    {
        CollectionPlan plan = CollectionPlan.Create(Company, Guid.NewGuid(), Guid.NewGuid());
        FakeCollectionPlanRepository plans = new FakeCollectionPlanRepository().Seed(plan);
        // Lab configuration knows no rooms, so any supplied room id is rejected.
        DefineSampleRoutingCommandHandler handler = new(plans, new FakeLabConfiguration());

        await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new DefineSampleRoutingCommand(
            plan.Id, SampleType.Blood, ["Hemograma"], Guid.NewGuid(), null, null, null)));
    }

    [Fact]
    public async Task DefineRouting_persists_the_matrix_row_with_a_known_room()
    {
        CollectionPlan plan = CollectionPlan.Create(Company, Guid.NewGuid(), Guid.NewGuid());
        Guid room = Guid.NewGuid();
        FakeCollectionPlanRepository plans = new FakeCollectionPlanRepository().Seed(plan);
        DefineSampleRoutingCommandHandler handler = new(plans, new FakeLabConfiguration().WithRoom(room));

        await handler.HandleAsync(new DefineSampleRoutingCommand(
            plan.Id, SampleType.Blood, ["Hemograma", "Bioquímica"], room, "−20 °C", -25m, -18m));

        SampleRouting routing = Assert.Single(plans.LastUpdated!.Routings);
        Assert.Equal(SampleType.Blood, routing.SampleType);
        Assert.Equal(room, routing.StorageRoomId);
        Assert.Equal(2, routing.PlannedAnalyses.Count);
        Assert.NotNull(routing.ConservationRange);
    }

    [Fact]
    public async Task AssignRole_rejects_an_unknown_role()
    {
        CollectionPlan plan = CollectionPlan.Create(Company, Guid.NewGuid(), Guid.NewGuid());
        Guid user = Guid.NewGuid();
        FakeCollectionPlanRepository plans = new FakeCollectionPlanRepository().Seed(plan);
        AssignCollectionRoleCommandHandler handler = new(
            plans,
            new FakeLabConfiguration(),                 // no roles cadastered
            new FakeCompanyMembershipQuery(user),
            new StubTenantContext(Company));

        await Assert.ThrowsAsync<BusinessException>(() =>
            handler.HandleAsync(new AssignCollectionRoleCommand(plan.Id, Guid.NewGuid(), user)));
    }

    [Fact]
    public async Task AssignRole_rejects_a_non_member()
    {
        CollectionPlan plan = CollectionPlan.Create(Company, Guid.NewGuid(), Guid.NewGuid());
        Guid role = Guid.NewGuid();
        Guid outsider = Guid.NewGuid();
        FakeCollectionPlanRepository plans = new FakeCollectionPlanRepository().Seed(plan);
        AssignCollectionRoleCommandHandler handler = new(
            plans,
            new FakeLabConfiguration().WithCollectionRole(role, "Anestesia"),
            new FakeCompanyMembershipQuery(Guid.NewGuid()),  // members set does NOT include the outsider
            new StubTenantContext(Company));

        await Assert.ThrowsAsync<BusinessException>(() =>
            handler.HandleAsync(new AssignCollectionRoleCommand(plan.Id, role, outsider)));
    }

    [Fact]
    public async Task AssignRole_persists_a_valid_assignment()
    {
        CollectionPlan plan = CollectionPlan.Create(Company, Guid.NewGuid(), Guid.NewGuid());
        Guid role = Guid.NewGuid();
        Guid user = Guid.NewGuid();
        FakeCollectionPlanRepository plans = new FakeCollectionPlanRepository().Seed(plan);
        AssignCollectionRoleCommandHandler handler = new(
            plans,
            new FakeLabConfiguration().WithCollectionRole(role, "Anestesia"),
            new FakeCompanyMembershipQuery(user),
            new StubTenantContext(Company));

        await handler.HandleAsync(new AssignCollectionRoleCommand(plan.Id, role, user));

        CollectionRoleAssignment assignment = Assert.Single(plans.LastUpdated!.Assignments);
        Assert.Equal(role, assignment.RoleId);
        Assert.Equal(user, assignment.UserId);
    }
}
