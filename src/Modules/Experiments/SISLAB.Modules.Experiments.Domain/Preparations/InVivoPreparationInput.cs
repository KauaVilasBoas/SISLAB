using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Preparations;

/// <summary>
/// The immutable inputs of an in vivo solution preparation (SISLAB-01): everything the operator would fill in by
/// hand on the spreadsheet to prepare what a dose group receives. A value object so a preparation is a pure
/// function of its inputs and can be frozen into a traceable snapshot.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here is specific to the current laboratory: the dose (g of compound per kg of animal), the
/// <see cref="GroupWeightGrams"/> the dose is computed against, the animal-weight basis the g:µL relation is
/// applied to (<see cref="RelationWeightGrams"/> — the spreadsheet rounds 189.6 g to 189 g for the 1 g : 5 µL
/// step), the <see cref="Ratio"/>, the compound <see cref="State"/> and, for liquids, its
/// <see cref="DensityGramsPerMillilitre"/> are all supplied per preparation / cadastrable per model.
/// </para>
/// <para>
/// The <b>vehicle-only</b> preparation (the Controle arm) is modelled by <see cref="IsVehicleOnly"/>: no compound
/// mass is computed and nothing is subtracted, so the final solution is entirely diluent (156 g → 780 µL of soy
/// oil in the spreadsheet).
/// </para>
/// </remarks>
public sealed class InVivoPreparationInput : ValueObject
{
    private InVivoPreparationInput(
        decimal doseAmountGramsPerKilogram,
        decimal groupWeightGrams,
        decimal relationWeightGrams,
        GramMicrolitreRatio ratio,
        CompoundState state,
        decimal? densityGramsPerMillilitre,
        bool isVehicleOnly)
    {
        DoseAmountGramsPerKilogram = doseAmountGramsPerKilogram;
        GroupWeightGrams = groupWeightGrams;
        RelationWeightGrams = relationWeightGrams;
        Ratio = ratio;
        State = state;
        DensityGramsPerMillilitre = densityGramsPerMillilitre;
        IsVehicleOnly = isVehicleOnly;
    }

    /// <summary>Dose expressed as grams of compound per kilogram of animal (e.g. 3 for 3 g/kg). Zero for vehicle.</summary>
    public decimal DoseAmountGramsPerKilogram { get; }

    /// <summary>Animal-weight basis (g) the dose is computed against — the summed/measured group weight.</summary>
    public decimal GroupWeightGrams { get; }

    /// <summary>Animal-weight basis (g) the g:µL relation is applied to, to size the final solution volume.</summary>
    public decimal RelationWeightGrams { get; }

    /// <summary>The animal-grams-to-µL relation that sizes the final solution volume.</summary>
    public GramMicrolitreRatio Ratio { get; }

    /// <summary>Physical state of the compound — decides density conversion and diluent subtraction.</summary>
    public CompoundState State { get; }

    /// <summary>Compound density (g/mL); required for a liquid compound, ignored for a powder / vehicle.</summary>
    public decimal? DensityGramsPerMillilitre { get; }

    /// <summary>True for the control/vehicle preparation: no compound, the solution is entirely diluent.</summary>
    public bool IsVehicleOnly { get; }

    /// <summary>
    /// Builds a treatment preparation input (a dosed arm), guarding the physical invariants: a positive dose and
    /// weights, and a positive density whenever the compound is a liquid (density is what turns its mass into the
    /// subtractable volume).
    /// </summary>
    public static InVivoPreparationInput ForTreatment(
        decimal doseAmountGramsPerKilogram,
        decimal groupWeightGrams,
        decimal relationWeightGrams,
        GramMicrolitreRatio ratio,
        CompoundState state,
        decimal? densityGramsPerMillilitre = null)
    {
        Guard.AgainstNonPositive(doseAmountGramsPerKilogram, nameof(doseAmountGramsPerKilogram));
        Guard.AgainstNonPositive(groupWeightGrams, nameof(groupWeightGrams));
        Guard.AgainstNonPositive(relationWeightGrams, nameof(relationWeightGrams));
        Guard.AgainstNull(ratio, nameof(ratio));

        if (state == CompoundState.Liquid)
        {
            if (densityGramsPerMillilitre is not { } density)
                throw new DomainException("A liquid compound requires its density (g/mL) to compute the compound volume.");

            Guard.AgainstNonPositive(density, nameof(densityGramsPerMillilitre));
        }

        return new InVivoPreparationInput(
            doseAmountGramsPerKilogram,
            groupWeightGrams,
            relationWeightGrams,
            ratio,
            state,
            state == CompoundState.Liquid ? densityGramsPerMillilitre : null,
            isVehicleOnly: false);
    }

    /// <summary>
    /// Builds a vehicle-only preparation (the Controle arm): only the g:µL relation and the animal-weight basis
    /// matter; the whole final solution is diluent, with no compound and no subtraction.
    /// </summary>
    public static InVivoPreparationInput ForVehicleOnly(decimal relationWeightGrams, GramMicrolitreRatio ratio)
    {
        Guard.AgainstNonPositive(relationWeightGrams, nameof(relationWeightGrams));
        Guard.AgainstNull(ratio, nameof(ratio));

        return new InVivoPreparationInput(
            doseAmountGramsPerKilogram: 0m,
            groupWeightGrams: relationWeightGrams,
            relationWeightGrams,
            ratio,
            CompoundState.Powder,
            densityGramsPerMillilitre: null,
            isVehicleOnly: true);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return DoseAmountGramsPerKilogram;
        yield return GroupWeightGrams;
        yield return RelationWeightGrams;
        yield return Ratio;
        yield return State;
        yield return DensityGramsPerMillilitre;
        yield return IsVehicleOnly;
    }
}
