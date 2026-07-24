using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Domain.Plates;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Domain;

/// <summary>
/// Domain tests for outlier exclusion on a plate (SISLAB-06): a well can be excluded/re-included with a recorded
/// reason and author while the experiment is not yet calculated, and both operations are rejected once the
/// snapshot is frozen (the replicate set is immutable for reproducibility).
/// </summary>
public sealed class WellExclusionTests
{
    [Fact]
    public void ExcludeWell_marks_the_well_as_excluded_with_reason_and_author()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(ExperimentTestData.FullyReadPlate(), "alice", DateTime.UtcNow);

        experiment.ExcludeWell("C1", "Outlier: 3 desvios acima das réplicas", "bob");

        Well excluded = experiment.Plate.Wells.Single(w => w.Coordinate == "C1");
        Assert.True(excluded.IsExcluded);
        Assert.False(excluded.CountsTowardCalculation);
        Assert.Equal("Outlier: 3 desvios acima das réplicas", excluded.ExclusionReason);
        Assert.Equal("bob", excluded.ExcludedBy);
    }

    [Fact]
    public void IncludeWell_brings_the_well_back_into_the_calculation()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(ExperimentTestData.FullyReadPlate(), "alice", DateTime.UtcNow);
        experiment.ExcludeWell("C1", "Outlier", "bob");

        experiment.IncludeWell("C1");

        Well included = experiment.Plate.Wells.Single(w => w.Coordinate == "C1");
        Assert.False(included.IsExcluded);
        Assert.True(included.CountsTowardCalculation);
        Assert.Null(included.ExclusionReason);
        Assert.Null(included.ExcludedBy);
    }

    [Fact]
    public void ExcludeWell_rejects_an_empty_reason()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(ExperimentTestData.FullyReadPlate(), "alice", DateTime.UtcNow);

        Assert.Throws<DomainException>(() => experiment.ExcludeWell("C1", "   ", "bob"));
    }

    [Fact]
    public void ExcludeWell_rejects_a_coordinate_outside_the_design()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(ExperimentTestData.FullyReadPlate(), "alice", DateTime.UtcNow);

        Assert.Throws<DomainException>(() => experiment.ExcludeWell("H12", "Outlier", "bob"));
    }

    [Fact]
    public void ExcludeWell_is_rejected_once_the_calculation_is_frozen()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(ExperimentTestData.FullyReadPlate(), "alice", DateTime.UtcNow);
        experiment.ApplyCalculation(
            FormulaSnapshot.Create("viability@v1", "expr", DateTime.UtcNow, "{}"), "bob");

        Assert.Throws<ConflictException>(() => experiment.ExcludeWell("C1", "Outlier", "bob"));
    }

    [Fact]
    public void IncludeWell_is_rejected_once_the_calculation_is_frozen()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(ExperimentTestData.FullyReadPlate(), "alice", DateTime.UtcNow);
        experiment.ApplyCalculation(
            FormulaSnapshot.Create("viability@v1", "expr", DateTime.UtcNow, "{}"), "bob");

        Assert.Throws<ConflictException>(() => experiment.IncludeWell("C1"));
    }

    [Fact]
    public void An_excluded_well_without_a_reading_does_not_block_readiness_to_calculate()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank, absorbance: 0.05m),
            ExperimentTestData.MakeWell('B', 1, WellRole.Control, absorbance: 1.05m),
            ExperimentTestData.MakeWell('C', 1, WellRole.Sample, absorbance: 0.55m),
            ExperimentTestData.MakeWell('D', 1, WellRole.Sample), // no reading
        ], "alice", DateTime.UtcNow);

        Assert.False(experiment.IsReadyToCalculate);

        experiment.ExcludeWell("D1", "Poço vazio / não lido", "bob");

        Assert.True(experiment.IsReadyToCalculate);
    }
}
