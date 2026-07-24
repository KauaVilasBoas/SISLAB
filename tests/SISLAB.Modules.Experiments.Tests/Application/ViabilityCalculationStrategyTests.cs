using System.Text.Json;
using SISLAB.Modules.Experiments.Application.Protocols;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Domain.Plates;
using SISLAB.Modules.Experiments.Tests.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Application;

public sealed class ViabilityCalculationStrategyTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Calculate_computes_percent_viability_against_control_minus_blank()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(ExperimentTestData.FullyReadPlate(), "tester", DateTime.UtcNow);

        FormulaSnapshot snapshot = new ViabilityCalculationStrategy().Calculate(experiment);

        Assert.Equal(ViabilityCalculationStrategy.FormulaCode, snapshot.FormulaName);

        using JsonDocument doc = JsonDocument.Parse(snapshot.ResultJson);
        JsonElement wells = doc.RootElement.GetProperty("wells");

        // (0.55 - 0.05) / (1.05 - 0.05) * 100 = 50.00
        JsonElement sample = wells.EnumerateArray().Single(w => w.GetProperty("well").GetString() == "D1");
        Assert.Equal(50.00m, sample.GetProperty("viabilityPct").GetDecimal());

        Assert.Equal(0.05m, doc.RootElement.GetProperty("blankMean").GetDecimal());
        Assert.Equal(1.05m, doc.RootElement.GetProperty("controlMean").GetDecimal());
    }

    [Fact]
    public void Calculate_rejects_a_plate_with_a_missing_absorbance()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank, absorbance: 0.05m),
            ExperimentTestData.MakeWell('B', 1, WellRole.Control, absorbance: 1.00m),
            ExperimentTestData.MakeWell('C', 1, WellRole.Sample), // no reading
        ], "tester", DateTime.UtcNow);

        DomainException ex = Assert.Throws<DomainException>(
            () => new ViabilityCalculationStrategy().Calculate(experiment));

        Assert.Contains("imported absorbance", ex.Message);
    }

    [Fact]
    public void Calculate_rejects_a_plate_with_no_blank()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Control, absorbance: 1.00m),
            ExperimentTestData.MakeWell('B', 1, WellRole.Sample, absorbance: 0.50m),
        ], "tester", DateTime.UtcNow);

        DomainException ex = Assert.Throws<DomainException>(
            () => new ViabilityCalculationStrategy().Calculate(experiment));

        Assert.Contains("blank", ex.Message);
    }

    [Fact]
    public void Calculate_rejects_a_zero_denominator_when_control_equals_blank()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank, absorbance: 0.50m),
            ExperimentTestData.MakeWell('B', 1, WellRole.Control, absorbance: 0.50m), // control == blank
            ExperimentTestData.MakeWell('C', 1, WellRole.Sample, absorbance: 0.50m),
        ], "tester", DateTime.UtcNow);

        DomainException ex = Assert.Throws<DomainException>(
            () => new ViabilityCalculationStrategy().Calculate(experiment));

        Assert.Contains("zero denominator", ex.Message);
    }

    [Fact]
    public void Excluding_a_control_outlier_changes_the_control_mean_and_the_result()
    {
        // Two controls: 1.00 and 1.10 (mean 1.05). Blank 0.05, sample 0.55.
        // Included:  (0.55 - 0.05) / (1.05 - 0.05) * 100 = 50.00%.
        // Exclude the 1.10 control → control mean 1.00 → (0.50) / (0.95) * 100 = 52.63%.
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(ExperimentTestData.FullyReadPlate(), "alice", DateTime.UtcNow);
        experiment.ExcludeWell("C1", "Controle destoante", "bob");

        FormulaSnapshot snapshot = new ViabilityCalculationStrategy().Calculate(experiment);

        using JsonDocument doc = JsonDocument.Parse(snapshot.ResultJson);
        Assert.Equal(1.00m, doc.RootElement.GetProperty("controlMean").GetDecimal());

        JsonElement sample = doc.RootElement.GetProperty("wells").EnumerateArray()
            .Single(w => w.GetProperty("well").GetString() == "D1");
        Assert.Equal(52.63m, sample.GetProperty("viabilityPct").GetDecimal());
    }

    [Fact]
    public void An_excluded_sample_well_is_absent_from_the_results()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(ExperimentTestData.FullyReadPlate(), "alice", DateTime.UtcNow);
        experiment.ExcludeWell("D1", "Réplica descartada", "bob");

        FormulaSnapshot snapshot = new ViabilityCalculationStrategy().Calculate(experiment);

        using JsonDocument doc = JsonDocument.Parse(snapshot.ResultJson);
        Assert.Empty(doc.RootElement.GetProperty("wells").EnumerateArray());
    }

    [Fact]
    public void Type_is_viability()
        => Assert.Equal(ExperimentType.ViabilidadeCelular, new ViabilityCalculationStrategy().Type);
}
