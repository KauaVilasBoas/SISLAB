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
    public void Type_is_viability()
        => Assert.Equal(ExperimentType.ViabilidadeCelular, new ViabilityCalculationStrategy().Type);
}
