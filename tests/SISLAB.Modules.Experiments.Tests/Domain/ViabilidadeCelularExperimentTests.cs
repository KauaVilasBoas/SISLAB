using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Domain.Experiments.Events;
using SISLAB.Modules.Experiments.Domain.Plates;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Domain;

public sealed class ViabilidadeCelularExperimentTests
{
    [Fact]
    public void Create_starts_in_draft_with_the_default_step_flow_and_raises_created()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment("MTT");

        Assert.Equal(ExperimentStatus.Draft, experiment.Status);
        Assert.Equal(ExperimentType.ViabilidadeCelular, experiment.Type);
        Assert.Equal(
            [ExperimentStepKind.Baseline, ExperimentStepKind.Measurement, ExperimentStepKind.Calculation, ExperimentStepKind.Analysis],
            experiment.Steps.Select(step => step.Kind));
        Assert.Contains(experiment.DomainEvents, e => e is ExperimentCreatedEvent);
    }

    [Fact]
    public void DesignPlate_moves_a_draft_into_progress_and_marks_the_baseline_step()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();

        experiment.DesignPlate(ExperimentTestData.FullyReadPlate(), "alice", DateTime.UtcNow);

        Assert.Equal(ExperimentStatus.InProgress, experiment.Status);
        Assert.True(experiment.Plate.IsDesigned);
        Assert.Equal("alice", experiment.FindStep(ExperimentStepKind.Baseline)!.PerformedBy);
    }

    [Fact]
    public void DesignPlate_rejects_duplicate_coordinates()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();

        Assert.Throws<DomainException>(() => experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank, absorbance: 0.1m),
            ExperimentTestData.MakeWell('A', 1, WellRole.Control, absorbance: 1.0m),
        ], "alice", DateTime.UtcNow));
    }

    [Fact]
    public void RecordWellAbsorbance_rejects_a_coordinate_outside_the_design()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(ExperimentTestData.FullyReadPlate(), "alice", DateTime.UtcNow);

        Assert.Throws<DomainException>(() => experiment.RecordWellAbsorbance("H12", 0.3m));
    }

    [Fact]
    public void ApplyCalculation_freezes_the_snapshot_advances_status_and_raises_calculated()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(ExperimentTestData.FullyReadPlate(), "alice", DateTime.UtcNow);

        FormulaSnapshot snapshot = FormulaSnapshot.Create("viability@v1", "expr", DateTime.UtcNow, "{}");
        experiment.ApplyCalculation(snapshot, "bob");

        Assert.Equal(ExperimentStatus.AwaitingAnalysis, experiment.Status);
        Assert.Same(snapshot, experiment.CalculationResult);
        Assert.Equal("bob", experiment.FindStep(ExperimentStepKind.Calculation)!.PerformedBy);
        Assert.Contains(experiment.DomainEvents, e => e is ExperimentCalculatedEvent);
    }

    [Fact]
    public void ApplyCalculation_is_rejected_once_already_calculated()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(ExperimentTestData.FullyReadPlate(), "alice", DateTime.UtcNow);
        experiment.ApplyCalculation(FormulaSnapshot.Create("viability@v1", "expr", DateTime.UtcNow, "{}"), "bob");

        Assert.Throws<ConflictException>(() =>
            experiment.ApplyCalculation(FormulaSnapshot.Create("viability@v1", "expr", DateTime.UtcNow, "{}"), "bob"));
    }

    [Fact]
    public void ApplyCalculation_requires_a_complete_reading()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank, absorbance: 0.05m),
            ExperimentTestData.MakeWell('B', 1, WellRole.Control), // no reading
        ], "alice", DateTime.UtcNow);

        Assert.Throws<DomainException>(() =>
            experiment.ApplyCalculation(FormulaSnapshot.Create("viability@v1", "expr", DateTime.UtcNow, "{}"), "bob"));
    }

    [Theory]
    [InlineData('I', 1)]
    [InlineData('A', 0)]
    [InlineData('A', 13)]
    public void Well_Create_guards_the_plate_bounds(char row, int column)
        => Assert.Throws<DomainException>(() => Well.Create(row, column, WellRole.Sample, null, null));
}
