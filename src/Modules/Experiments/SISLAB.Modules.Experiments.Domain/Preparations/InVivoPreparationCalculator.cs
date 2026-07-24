namespace SISLAB.Modules.Experiments.Domain.Preparations;

/// <summary>
/// The versioned in vivo preparation formula (SISLAB-01), the antidote to the by-hand dose-by-weight column of the
/// in vivo spreadsheet. Given an <see cref="InVivoPreparationInput"/> it computes the compound mass from the dose
/// and group weight, converts a liquid compound's mass to a volume via its density, sizes the final solution from
/// the g:µL relation and derives the diluent, returning an immutable <see cref="InVivoPreparationResult"/>.
/// </summary>
/// <remarks>
/// <para>
/// The formula is a single, pure calculation reused across laboratories — every laboratory-specific value (dose,
/// weights, g:µL relation, compound state, density, diluent) is an input, never a constant. It reproduces the
/// spreadsheet: <c>mass = dose(g/kg) × groupWeight(kg)</c>; <c>compoundVolume(µL) = mass ÷ density × 1000</c> for a
/// liquid; <c>finalVolume(µL) = relationWeight(g) × µL-per-g</c>; <c>diluent = finalVolume − compoundVolume</c> for
/// a liquid, else the whole final volume.
/// </para>
/// <para>
/// Kept as a domain calculator (not an <c>IExperimentProtocol</c>) because a preparation is not an experiment
/// reading; a persisting command wraps it into a traceable snapshot (author + date + parameters).
/// </para>
/// </remarks>
public static class InVivoPreparationCalculator
{
    /// <summary>Versioned formula code, in the <c>name@version</c> convention of the plate protocols.</summary>
    public const string FormulaCode = "invivo-preparation@v1";

    /// <summary>Human-readable expression of the formula, for the traceable snapshot.</summary>
    public const string FormulaExpression =
        "mass(g) = dose(g/kg) * groupWeight(kg); compoundVol(µL) = mass / density * 1000 (liquid); " +
        "finalVol(µL) = relationWeight(g) * µLPerGram; diluent(µL) = finalVol - compoundVol (liquid), else finalVol";

    private const int MassDecimals = 4;
    private const int VolumeDecimals = 2;

    /// <summary>
    /// Runs the preparation formula for the given inputs, returning the compound mass/volume, final solution volume
    /// and diluent volume. A vehicle-only preparation yields zero mass, no compound volume and an all-diluent
    /// solution; a powder yields a mass but no subtracted volume; a liquid applies density and subtracts.
    /// </summary>
    public static InVivoPreparationResult Calculate(InVivoPreparationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        decimal finalVolume = Round(input.Ratio.FinalVolumeMicrolitresFor(input.RelationWeightGrams), VolumeDecimals);

        if (input.IsVehicleOnly)
            return new InVivoPreparationResult(0m, compoundVolumeMicrolitres: null, finalVolume, finalVolume);

        // Keep full precision for the density conversion (the spreadsheet derives the µL volume from the exact
        // mass, then rounds); only the reported mass is rounded for the operator to weigh.
        decimal exactMass = input.DoseAmountGramsPerKilogram * input.GroupWeightGrams / 1000m;
        decimal compoundMass = Round(exactMass, MassDecimals);

        if (input.State == CompoundState.Powder)
            return new InVivoPreparationResult(compoundMass, compoundVolumeMicrolitres: null, finalVolume, finalVolume);

        decimal density = input.DensityGramsPerMillilitre!.Value;
        decimal compoundVolume = Round(exactMass / density * 1000m, VolumeDecimals);
        decimal diluent = Round(finalVolume - compoundVolume, VolumeDecimals);

        return new InVivoPreparationResult(compoundMass, compoundVolume, finalVolume, diluent);
    }

    private static decimal Round(decimal value, int decimals) => Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}
