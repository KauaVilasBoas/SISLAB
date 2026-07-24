using SISLAB.Modules.Experiments.Domain.Preparations;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Domain;

/// <summary>
/// Reproduces the by-hand mother-plate column of the in vitro spreadsheet (SISLAB-05). Factor, top concentration,
/// number of points and final volume are all inputs, never hard-coded.
/// </summary>
public sealed class SerialDilutionCalculatorTests
{
    [Fact]
    public void Build_reproduces_the_spreadsheet_factor4_series_at_600uL()
    {
        SerialDilutionScheme scheme = SerialDilutionCalculator.Build(
            topConcentrationMicromolar: 200m,
            factor: 4m,
            numberOfPoints: 6,
            finalVolumeMicrolitres: 600m);

        Assert.Equal(
            new[] { 200m, 50m, 12.5m, 3.125m, 0.781m, 0.195m },
            scheme.Concentrations);

        // Top point is prepared from the stock, not by transfer.
        SerialDilutionStep top = scheme.Steps[0];
        Assert.Null(top.TransferMicrolitres);
        Assert.Null(top.DiluentMicrolitres);

        // Every diluted point: transfer = 600 / 4 = 150; diluent = 600 - 150 = 450 (the spreadsheet's "600 -> 450").
        foreach (SerialDilutionStep step in scheme.Steps.Skip(1))
        {
            Assert.Equal(150m, step.TransferMicrolitres);
            Assert.Equal(450m, step.DiluentMicrolitres);
        }
    }

    [Fact]
    public void Build_supports_the_800uL_eppendorf_volume_for_oils()
    {
        SerialDilutionScheme scheme = SerialDilutionCalculator.Build(
            topConcentrationMicromolar: 200m,
            factor: 4m,
            numberOfPoints: 6,
            finalVolumeMicrolitres: 800m);

        // transfer = 800 / 4 = 200; diluent = 800 - 200 = 600.
        SerialDilutionStep diluted = scheme.Steps[1];
        Assert.Equal(200m, diluted.TransferMicrolitres);
        Assert.Equal(600m, diluted.DiluentMicrolitres);
        Assert.Equal(800m, diluted.FinalVolumeMicrolitres);
    }

    [Fact]
    public void Build_half_in_well_doubles_the_series_so_a_1_to_2_in_well_dilution_restores_it()
    {
        SerialDilutionScheme scheme = SerialDilutionCalculator.Build(
            topConcentrationMicromolar: 200m,
            factor: 4m,
            numberOfPoints: 3,
            finalVolumeMicrolitres: 600m,
            doubleForHalfInWell: true);

        Assert.Equal(new[] { 400m, 100m, 25m }, scheme.Concentrations);
    }

    [Fact]
    public void Build_rejects_a_factor_not_greater_than_one()
        => Assert.Throws<DomainException>(() => SerialDilutionCalculator.Build(200m, 1m, 6, 600m));

    [Fact]
    public void FromMolarMass_computes_the_stock_via_V_equals_m_M_over_MM()
    {
        // GDA-43 MM 612.716: 6.12716 mg to reach 10 mM (10000 µM) needs 1 mL -> 6.12716 mg/mL.
        StockSolution stock = StockSolution.FromMolarMass(
            massMilligrams: 6.12716m,
            molarMassGramsPerMole: 612.716m,
            targetMolarityMicromolar: 10000m);

        Assert.True(stock.HasMolarMass);
        Assert.Equal(10000m, stock.ConcentrationMicromolar);
        Assert.Equal(1m, Math.Round(stock.VolumeMillilitres, 6));
        Assert.Equal(6.12716m, Math.Round(stock.ConcentrationMilligramsPerMillilitre, 6));
    }

    [Fact]
    public void FromMassConcentration_handles_a_compound_without_molar_mass_in_mg_per_mL()
    {
        StockSolution stock = StockSolution.FromMassConcentration(
            concentrationMilligramsPerMillilitre: 5m,
            volumeMillilitres: 2m);

        Assert.False(stock.HasMolarMass);
        Assert.Null(stock.ConcentrationMicromolar);
        Assert.Equal(5m, stock.ConcentrationMilligramsPerMillilitre);
        Assert.Equal(10m, stock.MassMilligrams); // 5 mg/mL * 2 mL
    }

    [Fact]
    public void DmsoDilution_reproduces_300uL_in_1500uL_at_20_percent()
    {
        DmsoDilution dmso = DmsoDilution.Of(dmsoMicrolitres: 300m, solutionMicrolitres: 1500m);

        Assert.Equal(0.20m, dmso.SolutionFraction);
        Assert.Equal(0.20m, dmso.WellFraction);
    }

    [Fact]
    public void DmsoDilution_applies_the_in_well_dilution_ratio()
    {
        DmsoDilution dmso = DmsoDilution.Of(dmsoMicrolitres: 300m, solutionMicrolitres: 1500m, inWellDilutionRatio: 2m);

        Assert.Equal(0.20m, dmso.SolutionFraction);
        Assert.Equal(0.10m, dmso.WellFraction); // halved once applied to a half-medium well
    }
}
