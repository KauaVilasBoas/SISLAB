using SISLAB.Modules.Experiments.Application.Experiments.Commands;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Domain.Plates;
using SISLAB.Modules.Experiments.Tests.Domain;
using SISLAB.Modules.Experiments.Tests.Fakes;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Application;

public sealed class ExperimentCommandHandlerTests
{
    private static readonly FixedClock Clock = new(new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc));
    private static readonly FakeActorAccessor Actor = new("alice@lab");

    [Fact]
    public async Task Create_persists_a_draft_viability_experiment_with_the_actor_as_creator()
    {
        var experiments = new FakeExperimentRepository();
        var handler = new CreateExperimentCommandHandler(experiments, Actor, Clock);

        Guid id = await handler.HandleAsync(
            new CreateExperimentCommand(ExperimentType.ViabilidadeCelular, "MTT run", "desc", CompoundPartnerId: null));

        var created = Assert.IsType<ViabilidadeCelularExperiment>(experiments.LastAdded);
        Assert.Equal(id, created.Id);
        Assert.Equal("MTT run", created.Title);
        Assert.Equal("alice@lab", created.CreatedBy);
        Assert.Equal(ExperimentStatus.Draft, created.Status);
    }

    [Fact]
    public async Task Create_persists_a_draft_nitric_oxide_experiment_for_the_requested_type()
    {
        var experiments = new FakeExperimentRepository();
        var handler = new CreateExperimentCommandHandler(experiments, Actor, Clock);

        await handler.HandleAsync(
            new CreateExperimentCommand(ExperimentType.NitricOxide, "Griess run", null, CompoundPartnerId: null));

        var created = Assert.IsType<NitricOxideExperiment>(experiments.LastAdded);
        Assert.Equal(ExperimentType.NitricOxide, created.Type);
        Assert.Equal(ExperimentStatus.Draft, created.Status);
    }

    [Fact]
    public async Task DesignPlate_designs_the_plate_and_persists()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        var experiments = new FakeExperimentRepository().Seed(experiment);
        var handler = new DesignPlateCommandHandler(experiments, Actor, Clock);

        await handler.HandleAsync(new DesignPlateCommand(experiment.Id,
        [
            new PlateWellDefinition('A', 1, WellRole.Blank, null, null),
            new PlateWellDefinition('B', 1, WellRole.Control, null, null),
        ]));

        Assert.True(experiment.Plate.IsDesigned);
        Assert.Same(experiment, experiments.LastUpdated);
    }

    [Fact]
    public async Task DesignPlate_fails_when_the_experiment_does_not_exist()
    {
        var handler = new DesignPlateCommandHandler(new FakeExperimentRepository(), Actor, Clock);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(
            new DesignPlateCommand(Guid.NewGuid(), [new PlateWellDefinition('A', 1, WellRole.Blank, null, null)])));
    }

    [Fact]
    public async Task ImportReading_applies_absorbance_to_the_designed_wells()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank),
            ExperimentTestData.MakeWell('B', 1, WellRole.Control),
        ], "alice", Clock.UtcNow);
        var experiments = new FakeExperimentRepository().Seed(experiment);
        var handler = new ImportPlateReadingCommandHandler(experiments, Actor, Clock);

        await handler.HandleAsync(new ImportPlateReadingCommand(experiment.Id, "A1,0.05\nB1,1.00"));

        Assert.All(experiment.Plate.Wells, well => Assert.True(well.HasReading));
        Assert.Equal("alice@lab", experiment.FindStep(ExperimentStepKind.Measurement)!.PerformedBy);
    }

    [Fact]
    public async Task Calculate_runs_the_strategy_freezes_the_snapshot_and_advances_status()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(ExperimentTestData.FullyReadPlate(), "alice", Clock.UtcNow);
        var experiments = new FakeExperimentRepository().Seed(experiment);
        var handler = new CalculateExperimentCommandHandler(experiments, TestProtocols.Viability(), Actor);

        await handler.HandleAsync(new CalculateExperimentCommand(experiment.Id));

        Assert.NotNull(experiment.CalculationResult);
        Assert.Equal("viability@v1", experiment.CalculationResult!.FormulaName);
        Assert.Equal(ExperimentStatus.AwaitingAnalysis, experiment.Status);
        Assert.Same(experiment, experiments.LastUpdated);
    }
}
