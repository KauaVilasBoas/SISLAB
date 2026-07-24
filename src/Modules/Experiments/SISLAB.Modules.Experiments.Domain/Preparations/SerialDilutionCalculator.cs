using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Preparations;

/// <summary>
/// The versioned in vitro serial dilution formula (SISLAB-05), the antidote to the by-hand mother-plate column of
/// the in vitro spreadsheet. Given a top concentration, a dilution factor, a number of points and a fixed final
/// volume it generates the concentration series and the <c>C1·V1 = C2·V2</c> transfer/diluent volumes per point,
/// returning an immutable <see cref="SerialDilutionScheme"/>.
/// </summary>
/// <remarks>
/// <para>
/// The formula is a single, pure calculation: every laboratory-specific value (factor, top concentration, number of
/// points, final volume, and the "half in the well" doubling) is an input, never a constant. It reproduces the
/// spreadsheet series (factor 4, top 200 µM, 6 points): 200 → 50 → 12.5 → 3.125 → 0.781 → 0.195 µM, each point made
/// up to the fixed final volume with <c>transfer = finalVolume / factor</c> and <c>diluent = finalVolume −
/// transfer</c>.
/// </para>
/// <para>
/// The <b>half-in-the-well</b> adjustment (<c>doubleForHalfInWell</c>) prepares the mother series at twice the
/// intended concentration, because the operator later applies it to a well that already holds an equal volume of
/// medium — a 1:2 in-well dilution that brings each point back to its intended value.
/// </para>
/// </remarks>
public static class SerialDilutionCalculator
{
    /// <summary>Versioned formula code, in the <c>name@version</c> convention of the plate protocols.</summary>
    public const string FormulaCode = "serial-dilution@v1";

    /// <summary>Human-readable expression of the formula, for the traceable snapshot.</summary>
    public const string FormulaExpression =
        "C[i] = topConcentration / factor^i; transfer(µL) = finalVolume / factor; diluent(µL) = finalVolume − transfer";

    private const int ConcentrationDecimals = 3;
    private const int VolumeDecimals = 2;

    /// <summary>
    /// Builds the serial dilution scheme: <paramref name="numberOfPoints"/> concentrations from
    /// <paramref name="topConcentrationMicromolar"/> down by <paramref name="factor"/>, each point made up to
    /// <paramref name="finalVolumeMicrolitres"/>. When <paramref name="doubleForHalfInWell"/> is set, the whole
    /// series is doubled so a later 1:2 in-well dilution restores the intended concentrations.
    /// </summary>
    public static SerialDilutionScheme Build(
        decimal topConcentrationMicromolar,
        decimal factor,
        int numberOfPoints,
        decimal finalVolumeMicrolitres,
        bool doubleForHalfInWell = false)
    {
        Guard.AgainstNonPositive(topConcentrationMicromolar, nameof(topConcentrationMicromolar));
        Guard.AgainstNonPositive(finalVolumeMicrolitres, nameof(finalVolumeMicrolitres));

        if (factor <= 1m)
            throw new SharedKernel.Exceptions.DomainException(
                $"The dilution factor must be greater than 1. Received: {factor}.");

        if (numberOfPoints < 1)
            throw new SharedKernel.Exceptions.DomainException(
                $"The number of points must be at least 1. Received: {numberOfPoints}.");

        decimal top = doubleForHalfInWell ? topConcentrationMicromolar * 2m : topConcentrationMicromolar;

        decimal transfer = Round(finalVolumeMicrolitres / factor, VolumeDecimals);
        decimal diluent = Round(finalVolumeMicrolitres - transfer, VolumeDecimals);

        var steps = new List<SerialDilutionStep>(numberOfPoints);
        decimal concentration = top;

        for (int index = 0; index < numberOfPoints; index++)
        {
            bool isTop = index == 0;
            steps.Add(new SerialDilutionStep(
                index,
                Round(concentration, ConcentrationDecimals),
                isTop ? null : transfer,
                isTop ? null : diluent,
                finalVolumeMicrolitres));

            concentration /= factor;
        }

        return new SerialDilutionScheme(factor, finalVolumeMicrolitres, steps);
    }

    private static decimal Round(decimal value, int decimals) => Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}
