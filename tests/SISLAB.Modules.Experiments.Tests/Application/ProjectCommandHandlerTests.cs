using SISLAB.Modules.Experiments.Application.Projects.Commands;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.Modules.Experiments.Tests.Fakes;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Application;

public sealed class ProjectCommandHandlerTests
{
    [Fact]
    public async Task Create_persists_a_draft_project()
    {
        var projects = new FakeProjectRepository();
        var handler = new CreateProjectCommandHandler(projects);

        Guid id = await handler.HandleAsync(
            new CreateProjectCommand("Neuropatia — composto X", "Rattus norvegicus", "desc"));

        Project created = Assert.IsType<Project>(projects.LastAdded);
        Assert.Equal(id, created.Id);
        Assert.Equal("Neuropatia — composto X", created.Name);
        Assert.Equal(ProjectStatus.Draft, created.Status);
    }

    [Fact]
    public async Task AddBatch_adds_a_batch_and_persists()
    {
        Project project = Project.Create("P", "Rat");
        var projects = new FakeProjectRepository().Seed(project);
        var handler = new AddBatchCommandHandler(projects);

        Guid batchId = await handler.HandleAsync(new AddBatchCommand(project.Id, "Leva 1"));

        Assert.NotNull(projects.LastUpdated);
        Assert.Contains(projects.LastUpdated!.Batches, batch => batch.Id == batchId);
    }

    [Fact]
    public async Task AddBatch_on_a_missing_project_throws_not_found()
    {
        var handler = new AddBatchCommandHandler(new FakeProjectRepository());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new AddBatchCommand(Guid.NewGuid(), "Leva 1")));
    }

    [Fact]
    public async Task BindBatchToModel_validates_the_model_via_configuration_and_persists()
    {
        Project project = Project.Create("P", "Rat");
        Batch batch = project.AddBatch("Leva 1");
        var modelId = Guid.NewGuid();
        var projects = new FakeProjectRepository().Seed(project);
        var handler = new BindBatchToModelCommandHandler(projects, new FakeLabConfiguration(modelId));

        await handler.HandleAsync(new BindBatchToModelCommand(project.Id, batch.Id, modelId));

        Assert.Equal(modelId, projects.LastUpdated!.FindBatch(batch.Id).ExperimentalModelId);
    }

    [Fact]
    public async Task BindBatchToModel_rejects_a_model_that_does_not_exist_for_the_company()
    {
        Project project = Project.Create("P", "Rat");
        Batch batch = project.AddBatch("Leva 1");
        var projects = new FakeProjectRepository().Seed(project);
        // The fake knows a different model id, so the requested one is unknown for the tenant.
        var handler = new BindBatchToModelCommandHandler(projects, new FakeLabConfiguration(Guid.NewGuid()));

        await Assert.ThrowsAsync<BusinessException>(() =>
            handler.HandleAsync(new BindBatchToModelCommand(project.Id, batch.Id, Guid.NewGuid())));

        Assert.Null(projects.LastUpdated);
    }

    [Fact]
    public async Task BindBatchToModel_on_a_missing_project_throws_not_found()
    {
        var handler = new BindBatchToModelCommandHandler(
            new FakeProjectRepository(), new FakeLabConfiguration());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new BindBatchToModelCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())));
    }

    [Fact]
    public async Task StartBatch_freezes_the_batch_and_activates_the_project()
    {
        Project project = Project.Create("P", "Rat");
        Batch batch = project.AddBatch("Leva 1");
        Group control = project.AddGroup(batch.Id, "Controle", Dose.Of(0m, "mg/kg"));
        project.AddAnimal(batch.Id, control.Id, "M1-01", AnimalSex.Male);
        var projects = new FakeProjectRepository().Seed(project);
        var handler = new StartBatchCommandHandler(projects);

        await handler.HandleAsync(new StartBatchCommand(project.Id, batch.Id));

        Assert.Equal(BatchStatus.Running, projects.LastUpdated!.FindBatch(batch.Id).Status);
        Assert.Equal(ProjectStatus.Active, projects.LastUpdated!.Status);
    }
}
