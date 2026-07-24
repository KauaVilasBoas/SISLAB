using System.Text.Json;
using SISLAB.Modules.Experiments.Application.Protocols;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Domain.Plates;
using SISLAB.Modules.Experiments.Tests.Domain;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// SISLAB-07: replicates of the same condition (compound × concentration) are grouped into a per-condition mean
/// and sample standard deviation on the frozen snapshot. Covers the triplicate case, a ragged group (a missing
/// replicate) and integration with SISLAB-06 (an excluded replicate never enters the mean). The number of
/// replicates is not fixed at three.
/// </summary>
public sealed class ReplicateAggregationTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Control minus blank = 1.00 (denominator), so % viability = (abs − 0.05) × 100. The three 10 µM replicates
    /// at 0.90 / 0.89 / 0.91 give 85 / 84 / 86 %, mean 85, sample SD 1.
    /// </summary>
    private static ViabilidadeCelularExperiment TriplicateViability()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank, absorbance: 0.05m),
            ExperimentTestData.MakeWell('B', 1, WellRole.Control, absorbance: 1.05m),
            ExperimentTestData.MakeWell('A', 2, WellRole.Sample, absorbance: 0.90m, concentrationUm: 10m, sampleId: "GDA-43"),
            ExperimentTestData.MakeWell('B', 2, WellRole.Sample, absorbance: 0.89m, concentrationUm: 10m, sampleId: "GDA-43"),
            ExperimentTestData.MakeWell('C', 2, WellRole.Sample, absorbance: 0.91m, concentrationUm: 10m, sampleId: "GDA-43"),
        ], "alice", DateTime.UtcNow);
        return experiment;
    }

    [Fact]
    public void A_complete_triplicate_yields_mean_and_sample_standard_deviation()
    {
        FormulaSnapshot snapshot = new ViabilityCalculationStrategy().Calculate(TriplicateViability());

        Condition condition = Assert.Single(Conditions(snapshot));
        Assert.Equal("GDA-43", condition.SampleId);
        Assert.Equal(10m, condition.ConcentrationUm);
        Assert.Equal(3, condition.ReplicateCount);
        Assert.Equal(85.00m, condition.MeanViabilityPct);
        Assert.Equal(1.00m, condition.StdDevViabilityPct);
    }

    [Fact]
    public void A_condition_with_a_single_replicate_reports_a_null_standard_deviation()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank, absorbance: 0.05m),
            ExperimentTestData.MakeWell('B', 1, WellRole.Control, absorbance: 1.05m),
            ExperimentTestData.MakeWell('A', 2, WellRole.Sample, absorbance: 0.90m, concentrationUm: 10m, sampleId: "GDA-43"),
        ], "alice", DateTime.UtcNow);

        FormulaSnapshot snapshot = new ViabilityCalculationStrategy().Calculate(experiment);

        Condition condition = Assert.Single(Conditions(snapshot));
        Assert.Equal(1, condition.ReplicateCount);
        Assert.Equal(85.00m, condition.MeanViabilityPct);
        Assert.Null(condition.StdDevViabilityPct);
    }

    [Fact]
    public void A_missing_replicate_leaves_a_two_replicate_condition()
    {
        // Same triplicate but one well was never read and is excluded, so only two replicates remain.
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank, absorbance: 0.05m),
            ExperimentTestData.MakeWell('B', 1, WellRole.Control, absorbance: 1.05m),
            ExperimentTestData.MakeWell('A', 2, WellRole.Sample, absorbance: 0.90m, concentrationUm: 10m, sampleId: "GDA-43"),
            ExperimentTestData.MakeWell('B', 2, WellRole.Sample, absorbance: 0.88m, concentrationUm: 10m, sampleId: "GDA-43"),
            ExperimentTestData.MakeWell('C', 2, WellRole.Sample, concentrationUm: 10m, sampleId: "GDA-43"), // no reading
        ], "alice", DateTime.UtcNow);
        experiment.ExcludeWell("C2", "Poço não lido", "alice");

        FormulaSnapshot snapshot = new ViabilityCalculationStrategy().Calculate(experiment);

        Condition condition = Assert.Single(Conditions(snapshot));
        Assert.Equal(2, condition.ReplicateCount);
        // 85 and 83 → mean 84.
        Assert.Equal(84.00m, condition.MeanViabilityPct);
    }

    [Fact]
    public void An_excluded_replicate_does_not_enter_the_condition_mean()
    {
        // A triplicate where the third replicate is a wild outlier that is excluded (SISLAB-06). The mean must
        // reflect only the two that count, not the outlier.
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank, absorbance: 0.05m),
            ExperimentTestData.MakeWell('B', 1, WellRole.Control, absorbance: 1.05m),
            ExperimentTestData.MakeWell('A', 2, WellRole.Sample, absorbance: 0.90m, concentrationUm: 10m, sampleId: "GDA-43"),
            ExperimentTestData.MakeWell('B', 2, WellRole.Sample, absorbance: 0.90m, concentrationUm: 10m, sampleId: "GDA-43"),
            ExperimentTestData.MakeWell('C', 2, WellRole.Sample, absorbance: 0.20m, concentrationUm: 10m, sampleId: "GDA-43"), // outlier
        ], "alice", DateTime.UtcNow);
        experiment.ExcludeWell("C2", "Outlier visual", "alice");

        FormulaSnapshot snapshot = new ViabilityCalculationStrategy().Calculate(experiment);

        Condition condition = Assert.Single(Conditions(snapshot));
        Assert.Equal(2, condition.ReplicateCount);
        Assert.Equal(85.00m, condition.MeanViabilityPct); // both remaining replicates are 85%, not dragged down
        Assert.Equal(0m, condition.StdDevViabilityPct);
        Assert.DoesNotContain("C2", condition.Wells);
    }

    [Fact]
    public void Two_concentrations_of_the_same_compound_are_two_conditions()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank, absorbance: 0.05m),
            ExperimentTestData.MakeWell('B', 1, WellRole.Control, absorbance: 1.05m),
            ExperimentTestData.MakeWell('A', 2, WellRole.Sample, absorbance: 0.90m, concentrationUm: 200m, sampleId: "GDA-43"),
            ExperimentTestData.MakeWell('B', 2, WellRole.Sample, absorbance: 0.90m, concentrationUm: 200m, sampleId: "GDA-43"),
            ExperimentTestData.MakeWell('A', 3, WellRole.Sample, absorbance: 0.50m, concentrationUm: 50m, sampleId: "GDA-43"),
            ExperimentTestData.MakeWell('B', 3, WellRole.Sample, absorbance: 0.50m, concentrationUm: 50m, sampleId: "GDA-43"),
        ], "alice", DateTime.UtcNow);

        IReadOnlyList<Condition> conditions = Conditions(new ViabilityCalculationStrategy().Calculate(experiment));

        Assert.Equal(2, conditions.Count);
        Assert.Equal([50m, 200m], conditions.Select(c => c.ConcentrationUm)); // ascending concentration
    }

    [Fact]
    public void Nitric_oxide_replicates_of_the_same_sample_are_aggregated_by_computed_no()
    {
        NitricOxideExperiment experiment = ExperimentTestData.NewNitricOxideExperiment();
        // Curve slope 0.02, intercept 0 (baseline 0.05). Two sample replicates of "CTRL+" at raw 0.45 and 0.47
        // → corrected 0.40 / 0.42 → 20 and 21 µM NO. Mean 20.5, sample SD sqrt((0.25+0.25)/1)=0.7071.
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank, absorbance: 0.05m),
            ExperimentTestData.MakeWell('A', 2, WellRole.Standard, absorbance: 0.05m, concentrationUm: 0m),
            ExperimentTestData.MakeWell('B', 2, WellRole.Standard, absorbance: 0.25m, concentrationUm: 10m),
            ExperimentTestData.MakeWell('C', 2, WellRole.Standard, absorbance: 0.45m, concentrationUm: 20m),
            ExperimentTestData.MakeWell('A', 3, WellRole.Sample, absorbance: 0.45m, sampleId: "CTRL+"),
            ExperimentTestData.MakeWell('B', 3, WellRole.Sample, absorbance: 0.47m, sampleId: "CTRL+"),
        ], "alice", DateTime.UtcNow);

        FormulaSnapshot snapshot = new NitricOxideCalculationStrategy().Calculate(experiment);

        NoCondition condition = Assert.Single(
            JsonSerializer.Deserialize<NoPayload>(snapshot.ResultJson, Web)!.Conditions);
        Assert.Equal("CTRL+", condition.SampleId);
        Assert.Equal(2, condition.ReplicateCount);
        Assert.Equal(20.5m, condition.MeanConcentrationUm);
        Assert.Equal(0.7071m, condition.StdDevConcentrationUm);
    }

    private static IReadOnlyList<Condition> Conditions(FormulaSnapshot snapshot)
        => JsonSerializer.Deserialize<Payload>(snapshot.ResultJson, Web)!.Conditions;

    private sealed record Payload(IReadOnlyList<Condition> Conditions);

    private sealed record Condition(
        string? SampleId,
        decimal? ConcentrationUm,
        int ReplicateCount,
        decimal MeanViabilityPct,
        decimal? StdDevViabilityPct,
        IReadOnlyList<string> Wells);

    private sealed record NoPayload(IReadOnlyList<NoCondition> Conditions);

    private sealed record NoCondition(
        string? SampleId,
        int ReplicateCount,
        decimal MeanConcentrationUm,
        decimal? StdDevConcentrationUm);
}
