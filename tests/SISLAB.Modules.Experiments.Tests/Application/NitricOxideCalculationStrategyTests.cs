using System.Text.Json;
using SISLAB.Modules.Experiments.Application.Protocols;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Domain.Plates;
using SISLAB.Modules.Experiments.Tests.Domain;
using SISLAB.Modules.Experiments.Tests.Fakes;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// Unit tests for the <c>nitric-oxide@v1</c> Griess strategy: a clean linear curve inverts to the expected NO
/// concentration, a poor fit is flagged (warning, not error), and the validation guards (missing readings,
/// too few standards) fail fast.
/// </summary>
public sealed class NitricOxideCalculationStrategyTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Baseline-corrected curve absorbance = 0.02 * concentration (blank 0.05; standards 0.05 + 0.02*conc), so
    /// slope 0.02, intercept 0. A sample of raw absorbance 0.45 corrects to 0.40 → 0.40 / 0.02 = 20 µM.
    /// </summary>
    private static NitricOxideExperiment PerfectCurveExperiment(decimal sampleRawAbsorbance = 0.45m)
    {
        NitricOxideExperiment experiment = ExperimentTestData.NewNitricOxideExperiment();

        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank, absorbance: 0.05m),
            ExperimentTestData.MakeWell('A', 2, WellRole.Standard, absorbance: 0.05m, concentrationUm: 0m),
            ExperimentTestData.MakeWell('B', 2, WellRole.Standard, absorbance: 0.25m, concentrationUm: 10m),
            ExperimentTestData.MakeWell('C', 2, WellRole.Standard, absorbance: 0.45m, concentrationUm: 20m),
            ExperimentTestData.MakeWell('D', 2, WellRole.Standard, absorbance: 0.65m, concentrationUm: 30m),
            ExperimentTestData.MakeWell('A', 3, WellRole.Sample, absorbance: sampleRawAbsorbance),
        ], "alice", DateTime.UtcNow);

        return experiment;
    }

    [Fact]
    public void Calculate_fits_the_curve_and_reads_the_sample_concentration_off_the_line()
    {
        NitricOxideExperiment experiment = PerfectCurveExperiment();
        var strategy = new NitricOxideCalculationStrategy();

        FormulaSnapshot snapshot = strategy.Calculate(experiment);

        Assert.Equal("nitric-oxide@v1", snapshot.FormulaName);

        NoPayload payload = Deserialize(snapshot.ResultJson);
        Assert.Equal(0.02m, payload.Slope);
        Assert.Equal(0m, payload.Intercept);
        Assert.Equal(1m, payload.RSquared);
        Assert.False(payload.LowConfidence);

        NoWell sample = Assert.Single(payload.Wells);
        Assert.Equal("A3", sample.Well);
        Assert.Equal(20m, sample.ConcentrationUm);
    }

    [Fact]
    public void Calculate_flags_low_confidence_when_the_fit_is_poor_but_does_not_throw()
    {
        NitricOxideExperiment experiment = ExperimentTestData.NewNitricOxideExperiment();
        // A scattered, non-linear standard set drives R^2 well below 0.95 without being flat.
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank, absorbance: 0.00m),
            ExperimentTestData.MakeWell('A', 2, WellRole.Standard, absorbance: 0.10m, concentrationUm: 0m),
            ExperimentTestData.MakeWell('B', 2, WellRole.Standard, absorbance: 0.90m, concentrationUm: 10m),
            ExperimentTestData.MakeWell('C', 2, WellRole.Standard, absorbance: 0.20m, concentrationUm: 20m),
            ExperimentTestData.MakeWell('D', 2, WellRole.Standard, absorbance: 0.80m, concentrationUm: 30m),
            ExperimentTestData.MakeWell('A', 3, WellRole.Sample, absorbance: 0.50m),
        ], "alice", DateTime.UtcNow);

        FormulaSnapshot snapshot = new NitricOxideCalculationStrategy().Calculate(experiment);

        NoPayload payload = Deserialize(snapshot.ResultJson);
        Assert.True(payload.RSquared < NitricOxideCalculationStrategy.MinAcceptableRSquared);
        Assert.True(payload.LowConfidence);
    }

    [Fact]
    public void Calculate_rejects_a_plate_with_fewer_than_two_standards()
    {
        NitricOxideExperiment experiment = ExperimentTestData.NewNitricOxideExperiment();
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank, absorbance: 0.05m),
            ExperimentTestData.MakeWell('A', 2, WellRole.Standard, absorbance: 0.25m, concentrationUm: 10m),
            ExperimentTestData.MakeWell('A', 3, WellRole.Sample, absorbance: 0.45m),
        ], "alice", DateTime.UtcNow);

        Assert.Throws<DomainException>(() => new NitricOxideCalculationStrategy().Calculate(experiment));
    }

    [Fact]
    public void Calculate_rejects_a_plate_with_a_missing_reading()
    {
        NitricOxideExperiment experiment = ExperimentTestData.NewNitricOxideExperiment();
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank, absorbance: 0.05m),
            ExperimentTestData.MakeWell('A', 2, WellRole.Standard, absorbance: 0.25m, concentrationUm: 10m),
            ExperimentTestData.MakeWell('B', 2, WellRole.Standard, concentrationUm: 20m), // no reading
            ExperimentTestData.MakeWell('A', 3, WellRole.Sample, absorbance: 0.45m),
        ], "alice", DateTime.UtcNow);

        Assert.Throws<DomainException>(() => new NitricOxideCalculationStrategy().Calculate(experiment));
    }

    [Fact]
    public void Calculate_rejects_a_flat_calibration_curve()
    {
        NitricOxideExperiment experiment = ExperimentTestData.NewNitricOxideExperiment();
        // All standards at the same absorbance → zero slope, cannot invert to a concentration.
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank, absorbance: 0.00m),
            ExperimentTestData.MakeWell('A', 2, WellRole.Standard, absorbance: 0.30m, concentrationUm: 10m),
            ExperimentTestData.MakeWell('B', 2, WellRole.Standard, absorbance: 0.30m, concentrationUm: 20m),
            ExperimentTestData.MakeWell('A', 3, WellRole.Sample, absorbance: 0.30m),
        ], "alice", DateTime.UtcNow);

        Assert.Throws<DomainException>(() => new NitricOxideCalculationStrategy().Calculate(experiment));
    }

    [Fact]
    public void Excluding_a_standard_outlier_drops_it_from_the_curve_fit()
    {
        NitricOxideExperiment experiment = ExperimentTestData.NewNitricOxideExperiment();
        // A perfect line (slope 0.02) plus one wildly-off standard at 30 µM that would ruin the fit.
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Blank, absorbance: 0.05m),
            ExperimentTestData.MakeWell('A', 2, WellRole.Standard, absorbance: 0.05m, concentrationUm: 0m),
            ExperimentTestData.MakeWell('B', 2, WellRole.Standard, absorbance: 0.25m, concentrationUm: 10m),
            ExperimentTestData.MakeWell('C', 2, WellRole.Standard, absorbance: 0.45m, concentrationUm: 20m),
            ExperimentTestData.MakeWell('D', 2, WellRole.Standard, absorbance: 5.00m, concentrationUm: 30m), // outlier
            ExperimentTestData.MakeWell('A', 3, WellRole.Sample, absorbance: 0.45m),
        ], "alice", DateTime.UtcNow);
        experiment.ExcludeWell("D2", "Padrão fora da curva", "bob");

        FormulaSnapshot snapshot = new NitricOxideCalculationStrategy().Calculate(experiment);

        NoPayload payload = Deserialize(snapshot.ResultJson);
        // With the outlier gone the remaining three standards are the clean 0.02x line again.
        Assert.Equal(0.02m, payload.Slope);
        Assert.Equal(1m, payload.RSquared);
        NoWell sample = Assert.Single(payload.Wells);
        Assert.Equal(20m, sample.ConcentrationUm);
    }

    [Fact]
    public void Resolver_returns_the_nitric_oxide_strategy_for_the_nitric_oxide_type()
    {
        IExperimentProtocol protocol = TestProtocols.All().Resolve(ExperimentType.NitricOxide);

        Assert.IsType<NitricOxideCalculationStrategy>(protocol);
        Assert.Equal(ExperimentType.NitricOxide, protocol.Type);
    }

    private static NoPayload Deserialize(string json)
        => JsonSerializer.Deserialize<NoPayload>(json, Json)!;

    private sealed record NoPayload(
        decimal Slope,
        decimal Intercept,
        decimal RSquared,
        bool LowConfidence,
        decimal BlankBaseline,
        IReadOnlyList<NoWell> Wells);

    private sealed record NoWell(string Well, decimal ConcentrationUm);
}
