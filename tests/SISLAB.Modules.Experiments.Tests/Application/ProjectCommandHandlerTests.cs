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
    public async Task RecordPhysiologicalReading_persists_the_reading_with_the_actor_and_clock()
    {
        Project project = Project.Create("P", "Rat");
        Batch batch = project.AddBatch("Leva 1");
        Cage cage = project.AddCage(batch.Id, "CX1", capacity: 4);
        Animal animal = project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A1", AnimalSex.Male);
        var projects = new FakeProjectRepository().Seed(project);
        var when = new DateTime(2026, 7, 24, 9, 0, 0, DateTimeKind.Utc);
        var handler = new RecordPhysiologicalReadingCommandHandler(
            projects, new FakeActorAccessor("vic@lab"), new FixedClock(when));

        Guid id = await handler.HandleAsync(new RecordPhysiologicalReadingCommand(
            project.Id, animal.Id, "glicemia", 268m, "mg/dL", "pós-indução"));

        PhysiologicalReading reading = Assert.Single(projects.LastUpdated!.PhysiologicalReadings);
        Assert.Equal(id, reading.Id);
        // The author and instant come from the accessor/clock, never the request body.
        Assert.Equal("vic@lab", reading.RecordedBy);
        Assert.Equal(when, reading.RecordedAtUtc);
    }

    [Fact]
    public async Task RecordPhysiologicalReading_on_a_missing_project_throws_not_found()
    {
        var handler = new RecordPhysiologicalReadingCommandHandler(
            new FakeProjectRepository(), new FakeActorAccessor(), new FixedClock(DateTime.UtcNow));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(
            new RecordPhysiologicalReadingCommand(
                Guid.NewGuid(), Guid.NewGuid(), "glicemia", 268m, "mg/dL", "basal")));
    }

    [Fact]
    public async Task StartBatch_freezes_the_batch_and_activates_the_project()
    {
        Project project = Project.Create("P", "Rat");
        Batch batch = project.AddBatch("Leva 1");
        Cage cage = project.AddCage(batch.Id, "CX1", capacity: 4);
        project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A1", AnimalSex.Male);
        var projects = new FakeProjectRepository().Seed(project);
        var handler = new StartBatchCommandHandler(projects);

        await handler.HandleAsync(new StartBatchCommand(project.Id, batch.Id));

        Assert.Equal(BatchStatus.Running, projects.LastUpdated!.FindBatch(batch.Id).Status);
        Assert.Equal(ProjectStatus.Active, projects.LastUpdated!.Status);
    }

    [Fact]
    public async Task AddCage_adds_a_cage_and_persists()
    {
        Project project = Project.Create("P", "Rat");
        Batch batch = project.AddBatch("Leva 1");
        var projects = new FakeProjectRepository().Seed(project);
        var handler = new AddCageCommandHandler(projects);

        Guid cageId = await handler.HandleAsync(new AddCageCommand(project.Id, batch.Id, "CX1", 4));

        Assert.NotNull(projects.LastUpdated);
        Cage cage = Assert.Single(projects.LastUpdated!.FindBatch(batch.Id).Cages);
        Assert.Equal(cageId, cage.Id);
        Assert.Equal(4, cage.Capacity);
    }

    [Fact]
    public async Task AddAnimal_houses_the_animal_in_a_cage_without_a_group()
    {
        Project project = Project.Create("P", "Rat");
        Batch batch = project.AddBatch("Leva 1");
        Cage cage = project.AddCage(batch.Id, "CX1", capacity: 4);
        var projects = new FakeProjectRepository().Seed(project);
        var handler = new AddAnimalCommandHandler(projects);

        Guid animalId = await handler.HandleAsync(
            new AddAnimalCommand(project.Id, batch.Id, cage.Id, "CX1-A1", AnimalSex.Male, 189.6m));

        Animal animal = Assert.Single(projects.LastUpdated!.FindBatch(batch.Id).Animals);
        Assert.Equal(animalId, animal.Id);
        Assert.Null(animal.GroupId);
    }

    [Fact]
    public async Task AssignAnimalToGroup_assigns_and_persists()
    {
        Project project = Project.Create("P", "Rat");
        Batch batch = project.AddBatch("Leva 1");
        Group dose = project.AddGroup(batch.Id, "Dose 3", Dose.Of(3m, "g/kg"));
        Cage cage = project.AddCage(batch.Id, "CX1", capacity: 4);
        Animal animal = project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A1", AnimalSex.Male);
        var projects = new FakeProjectRepository().Seed(project);
        var handler = new AssignAnimalToGroupCommandHandler(projects);

        await handler.HandleAsync(new AssignAnimalToGroupCommand(project.Id, batch.Id, animal.Id, dose.Id));

        Assert.Equal(dose.Id, projects.LastUpdated!.FindBatch(batch.Id).Animals.Single().GroupId);
    }
}
