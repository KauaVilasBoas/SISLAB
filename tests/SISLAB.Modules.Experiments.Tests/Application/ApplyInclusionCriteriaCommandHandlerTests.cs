using SISLAB.Modules.Configuration.Contracts;
using SISLAB.Modules.Experiments.Application.Projects.Commands;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.Modules.Experiments.Tests.Fakes;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// Covers the <see cref="ApplyInclusionCriteriaCommandHandler"/> (SISLAB-02): it reads the batch's model applicable
/// parameters and the tenant's inclusion criteria through the Configuration port, adapts them and applies the
/// selection on the aggregate. The three acceptance cases — inclusion ≥ limiar, exclusion &lt; limiar, and an
/// inapplicable parameter that does not block — are exercised through the real command path.
/// </summary>
public sealed class ApplyInclusionCriteriaCommandHandlerTests
{
    private static readonly DateTime When = new(2026, 7, 24, 9, 0, 0, DateTimeKind.Utc);

    private static readonly InclusionCriterionDto GlicemiaGte250 =
        new("glicemia", "GreaterThanOrEqual", 250m, "mg/dL");

    private static (Project project, Guid batchId, Animal animal) BoundBatchWithAnimal(Guid modelId, decimal glicemia)
    {
        Project project = Project.Create("Neuropatia diabética", "Rattus norvegicus");
        Batch batch = project.AddBatch("Leva 1");
        project.BindBatchToModel(batch.Id, modelId);
        Group group = project.AddGroup(batch.Id, "Curva", Dose.Of(3m, "g/kg"));
        Animal animal = project.AddAnimal(batch.Id, group.Id, "M1-01", AnimalSex.Male);
        project.RecordPhysiologicalReading(animal.Id, "glicemia", glicemia, "mg/dL", "pós-indução", "vic@lab", When);
        return (project, batch.Id, animal);
    }

    [Fact]
    public async Task Applies_the_criterion_and_includes_an_animal_at_or_above_the_threshold()
    {
        var modelId = Guid.NewGuid();
        (Project project, Guid batchId, Animal animal) = BoundBatchWithAnimal(modelId, glicemia: 268m);
        var projects = new FakeProjectRepository().Seed(project);
        var lab = new FakeLabConfiguration().WithModel(modelId, "glicemia", "peso").WithCriteria(GlicemiaGte250);
        var handler = new ApplyInclusionCriteriaCommandHandler(projects, lab);

        int decided = await handler.HandleAsync(new ApplyInclusionCriteriaCommand(project.Id, batchId));

        Assert.Equal(1, decided);
        Assert.Equal(AnimalInclusionStatus.Included, animal.Inclusion!.Status);
        Assert.Equal(268m, animal.Inclusion.DecidingValue);
        Assert.NotNull(projects.LastUpdated);
    }

    [Fact]
    public async Task Applies_the_criterion_and_excludes_an_animal_below_the_threshold()
    {
        var modelId = Guid.NewGuid();
        (Project project, Guid batchId, Animal animal) = BoundBatchWithAnimal(modelId, glicemia: 214m);
        var projects = new FakeProjectRepository().Seed(project);
        var lab = new FakeLabConfiguration().WithModel(modelId, "glicemia").WithCriteria(GlicemiaGte250);
        var handler = new ApplyInclusionCriteriaCommandHandler(projects, lab);

        await handler.HandleAsync(new ApplyInclusionCriteriaCommand(project.Id, batchId));

        Assert.Equal(AnimalInclusionStatus.Excluded, animal.Inclusion!.Status);
        Assert.Equal(214m, animal.Inclusion.DecidingValue);
    }

    [Fact]
    public async Task A_parameter_not_applicable_to_the_model_does_not_block_the_animal()
    {
        var modelId = Guid.NewGuid();
        // Reading would be excluded, but the model (non-diabetic) does not declare glicemia applicable.
        (Project project, Guid batchId, Animal animal) = BoundBatchWithAnimal(modelId, glicemia: 100m);
        var projects = new FakeProjectRepository().Seed(project);
        var lab = new FakeLabConfiguration().WithModel(modelId, "peso", "rotarod").WithCriteria(GlicemiaGte250);
        var handler = new ApplyInclusionCriteriaCommandHandler(projects, lab);

        int decided = await handler.HandleAsync(new ApplyInclusionCriteriaCommand(project.Id, batchId));

        Assert.Equal(0, decided);
        Assert.Null(animal.Inclusion);
    }

    [Fact]
    public async Task A_batch_with_no_model_bound_decides_nothing()
    {
        Project project = Project.Create("Neuropatia diabética", "Rattus norvegicus");
        Batch batch = project.AddBatch("Leva 1");
        Group group = project.AddGroup(batch.Id, "Curva", Dose.Of(3m, "g/kg"));
        Animal animal = project.AddAnimal(batch.Id, group.Id, "M1-01", AnimalSex.Male);
        project.RecordPhysiologicalReading(animal.Id, "glicemia", 268m, "mg/dL", "pós-indução", "vic@lab", When);
        var projects = new FakeProjectRepository().Seed(project);
        var lab = new FakeLabConfiguration().WithCriteria(GlicemiaGte250);
        var handler = new ApplyInclusionCriteriaCommandHandler(projects, lab);

        int decided = await handler.HandleAsync(new ApplyInclusionCriteriaCommand(project.Id, batch.Id));

        Assert.Equal(0, decided);
        Assert.Null(animal.Inclusion);
    }
}
