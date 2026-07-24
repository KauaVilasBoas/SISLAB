using SISLAB.Modules.Experiments.Domain.Preparations;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Domain;

/// <summary>
/// Reproduces the by-hand dose-by-weight column of the in vivo spreadsheet (SISLAB-01). Every laboratory-specific
/// value (dose, group weight, g:µL relation, density, diluent) is passed as an input, never hard-coded.
/// </summary>
public sealed class InVivoPreparationCalculatorTests
{
    [Fact]
    public void Calculate_reproduces_the_spreadsheet_3g_per_kg_liquid_case()
    {
        // 3 g/kg, group 189.6 g, density 0.9865 g/mL, relation 1 g : 5 µL on a 189 g basis.
        InVivoPreparationInput input = InVivoPreparationInput.ForTreatment(
            doseAmountGramsPerKilogram: 3m,
            groupWeightGrams: 189.6m,
            relationWeightGrams: 189m,
            ratio: GramMicrolitreRatio.OfGramToMicrolitres(5m),
            state: CompoundState.Liquid,
            densityGramsPerMillilitre: 0.9865m);

        InVivoPreparationResult result = InVivoPreparationCalculator.Calculate(input);

        Assert.Equal(0.5688m, result.CompoundMassGrams);
        Assert.Equal(576.58m, result.CompoundVolumeMicrolitres);
        Assert.Equal(945m, result.FinalVolumeMicrolitres);
        Assert.Equal(368.42m, result.DiluentVolumeMicrolitres);
    }

    [Fact]
    public void Calculate_reproduces_the_spreadsheet_0_6g_per_kg_liquid_case()
    {
        // 0.6 g/kg on the same base compound: 114.59 µL of OGV, 827.41 µL of diluent, final 942 µL.
        InVivoPreparationInput input = InVivoPreparationInput.ForTreatment(
            doseAmountGramsPerKilogram: 0.6m,
            groupWeightGrams: 188.4m,
            relationWeightGrams: 188.4m,
            ratio: GramMicrolitreRatio.OfGramToMicrolitres(5m),
            state: CompoundState.Liquid,
            densityGramsPerMillilitre: 0.9865m);

        InVivoPreparationResult result = InVivoPreparationCalculator.Calculate(input);

        // mass = 0.6 * 188.4 / 1000 = 0.11304 g; vol = 0.11304 / 0.9865 * 1000 = 114.59 µL; final = 188.4 * 5 = 942.
        Assert.Equal(0.1130m, result.CompoundMassGrams);
        Assert.Equal(114.59m, result.CompoundVolumeMicrolitres);
        Assert.Equal(942m, result.FinalVolumeMicrolitres);
        Assert.Equal(827.41m, result.DiluentVolumeMicrolitres);
    }

    [Fact]
    public void Calculate_vehicle_only_control_makes_an_all_diluent_solution_without_subtraction()
    {
        // Controle: 156 g → 780 µL of soy oil (1 g : 5 µL), no compound, no subtraction.
        InVivoPreparationInput input = InVivoPreparationInput.ForVehicleOnly(
            relationWeightGrams: 156m,
            ratio: GramMicrolitreRatio.OfGramToMicrolitres(5m));

        InVivoPreparationResult result = InVivoPreparationCalculator.Calculate(input);

        Assert.Equal(0m, result.CompoundMassGrams);
        Assert.Null(result.CompoundVolumeMicrolitres);
        Assert.Equal(780m, result.FinalVolumeMicrolitres);
        Assert.Equal(780m, result.DiluentVolumeMicrolitres);
    }

    [Fact]
    public void Calculate_powder_compound_computes_mass_but_does_not_subtract_a_volume()
    {
        InVivoPreparationInput input = InVivoPreparationInput.ForTreatment(
            doseAmountGramsPerKilogram: 3m,
            groupWeightGrams: 189.6m,
            relationWeightGrams: 189m,
            ratio: GramMicrolitreRatio.OfGramToMicrolitres(5m),
            state: CompoundState.Powder);

        InVivoPreparationResult result = InVivoPreparationCalculator.Calculate(input);

        Assert.Equal(0.5688m, result.CompoundMassGrams);
        Assert.Null(result.CompoundVolumeMicrolitres);
        Assert.Equal(945m, result.FinalVolumeMicrolitres);
        Assert.Equal(945m, result.DiluentVolumeMicrolitres); // no subtraction for a powder
    }

    [Fact]
    public void Calculate_general_default_relation_1_to_10_is_just_another_parameter()
    {
        InVivoPreparationInput input = InVivoPreparationInput.ForVehicleOnly(
            relationWeightGrams: 200m,
            ratio: GramMicrolitreRatio.OfGramToMicrolitres(10m));

        InVivoPreparationResult result = InVivoPreparationCalculator.Calculate(input);

        Assert.Equal(2000m, result.FinalVolumeMicrolitres);
    }

    [Fact]
    public void ForTreatment_requires_density_for_a_liquid_compound()
    {
        DomainException ex = Assert.Throws<DomainException>(() => InVivoPreparationInput.ForTreatment(
            doseAmountGramsPerKilogram: 3m,
            groupWeightGrams: 189.6m,
            relationWeightGrams: 189m,
            ratio: GramMicrolitreRatio.OfGramToMicrolitres(5m),
            state: CompoundState.Liquid,
            densityGramsPerMillilitre: null));

        Assert.Contains("density", ex.Message);
    }

    [Fact]
    public void OfGramToMicrolitres_rejects_a_non_positive_relation()
        => Assert.Throws<DomainException>(() => GramMicrolitreRatio.OfGramToMicrolitres(0m));
}
