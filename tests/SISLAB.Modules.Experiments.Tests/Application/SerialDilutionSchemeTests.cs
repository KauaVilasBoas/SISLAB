using SISLAB.Modules.Experiments.Application.Experiments.Commands;
using SISLAB.Modules.Experiments.Application.Experiments.Queries;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Domain.Plates;
using SISLAB.Modules.Experiments.Tests.Domain;
using SISLAB.Modules.Experiments.Tests.Fakes;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// Covers the SISLAB-05 wiring: the stateless <see cref="ComputeSerialDilutionSchemeQueryHandler"/> (series + volumes
/// + optional stock/DMSO) and the <see cref="ApplyDilutionSchemeCommandHandler"/> that populates the plate wells'
/// <c>ConcentrationUm</c> from the computed series. Both reuse the pure calculators the domain tests already pin.
/// </summary>
public sealed class SerialDilutionSchemeTests
{
    private static readonly FixedClock Clock = new(new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc));
    private static readonly FakeActorAccessor Actor = new("alice@lab");
    private static readonly FakeCurrentUserContext CurrentUser = new(Guid.NewGuid());

    [Fact]
    public async Task Compute_reproduces_the_spreadsheet_factor_4_series_and_volumes()
    {
        var handler = new ComputeSerialDilutionSchemeQueryHandler();

        // Factor 4, top 200 µM, 6 points, final 600 µL → 200 → 50 → 12.5 → 3.125 → 0.781 → 0.195 µM.
        SerialDilutionSchemeResult result = await handler.HandleAsync(
            new ComputeSerialDilutionSchemeQuery(200m, 4m, 6, 600m));

        Assert.Equal(new[] { 200m, 50m, 12.5m, 3.125m, 0.781m, 0.195m },
            result.Steps.Select(step => step.ConcentrationMicromolar).ToArray());

        // Non-top points transfer finalVolume/factor and top up with diluent (C1V1=C2V2): 600/4 = 150, 600-150 = 450.
        Assert.Null(result.Steps[0].TransferMicrolitres);
        Assert.All(result.Steps.Skip(1), step =>
        {
            Assert.Equal(150m, step.TransferMicrolitres);
            Assert.Equal(450m, step.DiluentMicrolitres);
        });
        Assert.Null(result.Stock);
        Assert.Null(result.Dmso);
    }

    [Fact]
    public async Task Compute_supports_the_800ul_eppendorf_volume_for_oils()
    {
        var handler = new ComputeSerialDilutionSchemeQueryHandler();

        SerialDilutionSchemeResult result = await handler.HandleAsync(
            new ComputeSerialDilutionSchemeQuery(200m, 4m, 6, 800m));

        // 800 µL eppendorf: transfer 800/4 = 200, diluent 800-200 = 600.
        Assert.Equal(800m, result.FinalVolumeMicrolitres);
        Assert.All(result.Steps.Skip(1), step =>
        {
            Assert.Equal(200m, step.TransferMicrolitres);
            Assert.Equal(600m, step.DiluentMicrolitres);
        });
    }

    [Fact]
    public async Task Compute_builds_the_stock_from_a_mass_concentration_for_a_compound_without_molar_mass()
    {
        var handler = new ComputeSerialDilutionSchemeQueryHandler();

        SerialDilutionSchemeResult result = await handler.HandleAsync(
            new ComputeSerialDilutionSchemeQuery(200m, 4m, 6, 600m)
            {
                // A compound with no molar mass → mg/mL route: 2 mg/mL made up to 1 mL.
                StockConcentrationMilligramsPerMillilitre = 2m,
                StockVolumeMillilitres = 1m,
            });

        Assert.NotNull(result.Stock);
        Assert.Null(result.Stock!.MolarMassGramsPerMole);
        Assert.Null(result.Stock.ConcentrationMicromolar);
        Assert.Equal(2m, result.Stock.ConcentrationMilligramsPerMillilitre);
        Assert.Equal(2m, result.Stock.MassMilligrams);
    }

    [Fact]
    public async Task Compute_builds_the_stock_and_dmso_control_when_supplied()
    {
        var handler = new ComputeSerialDilutionSchemeQueryHandler();

        SerialDilutionSchemeResult result = await handler.HandleAsync(
            new ComputeSerialDilutionSchemeQuery(200m, 4m, 6, 600m)
            {
                StockMassMilligrams = 1m,
                StockMolarMassGramsPerMole = 612.716m,
                StockTargetMolarityMicromolar = 200m,
                DmsoMicrolitres = 300m,
                DmsoSolutionMicrolitres = 1500m,
            });

        Assert.NotNull(result.Stock);
        Assert.Equal(612.716m, result.Stock!.MolarMassGramsPerMole);
        Assert.Equal(200m, result.Stock.ConcentrationMicromolar);

        Assert.NotNull(result.Dmso);
        Assert.Equal(0.2m, result.Dmso!.SolutionFraction); // 300 / 1500
    }

    [Fact]
    public async Task ApplyScheme_populates_the_column_wells_concentration_from_the_series()
    {
        // A designed column A1..F1 with 6 wells and no concentrations yet.
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Sample),
            ExperimentTestData.MakeWell('B', 1, WellRole.Sample),
            ExperimentTestData.MakeWell('C', 1, WellRole.Sample),
            ExperimentTestData.MakeWell('D', 1, WellRole.Sample),
            ExperimentTestData.MakeWell('E', 1, WellRole.Sample),
            ExperimentTestData.MakeWell('F', 1, WellRole.Sample),
        ], "alice", Clock.UtcNow);

        var experiments = new FakeExperimentRepository().Seed(experiment);
        var handler = new ApplyDilutionSchemeCommandHandler(experiments, CurrentUser);

        await handler.HandleAsync(new ApplyDilutionSchemeCommand(
            experiment.Id, Column: 1,
            TopConcentrationMicromolar: 200m, Factor: 4m, NumberOfPoints: 6, FinalVolumeMicrolitres: 600m,
            DoubleForHalfInWell: false));

        // The column's wells (A→F) now carry the series concentrations, top row down.
        decimal?[] byRow = experiment.Plate.Wells
            .Where(well => well.Column == 1)
            .OrderBy(well => well.Row)
            .Select(well => well.ConcentrationUm)
            .ToArray();

        Assert.Equal(new decimal?[] { 200m, 50m, 12.5m, 3.125m, 0.781m, 0.195m }, byRow);
        Assert.Same(experiment, experiments.LastUpdated);
    }

    [Fact]
    public async Task ApplyScheme_rejects_a_column_whose_well_count_does_not_match_the_series()
    {
        // A column with only 2 wells cannot receive a 6-point series.
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.DesignPlate(
        [
            ExperimentTestData.MakeWell('A', 1, WellRole.Sample),
            ExperimentTestData.MakeWell('B', 1, WellRole.Sample),
        ], "alice", Clock.UtcNow);

        var experiments = new FakeExperimentRepository().Seed(experiment);
        var handler = new ApplyDilutionSchemeCommandHandler(experiments, CurrentUser);

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(new ApplyDilutionSchemeCommand(
            experiment.Id, 1, 200m, 4m, 6, 600m, false)));
    }

    [Fact]
    public async Task ApplyScheme_on_a_missing_experiment_throws_not_found()
    {
        var handler = new ApplyDilutionSchemeCommandHandler(new FakeExperimentRepository(), CurrentUser);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(new ApplyDilutionSchemeCommand(
            Guid.NewGuid(), 1, 200m, 4m, 6, 600m, false)));
    }
}
