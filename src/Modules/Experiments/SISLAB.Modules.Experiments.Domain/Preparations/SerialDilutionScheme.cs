using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Experiments.Domain.Preparations;

/// <summary>
/// The full serial dilution scheme of an in vitro plate (SISLAB-05): the ordered points of the mother plate, from
/// the top concentration down by the dilution factor, each with its transfer/diluent volumes. A value object that
/// callers can freeze into a traceable snapshot and use to populate the plate's <c>ConcentrationUm</c> wells.
/// </summary>
/// <remarks>
/// Reproduces the spreadsheet series (factor 4, top 200 µM, 6 points): 200 → 50 → 12.5 → 3.125 → 0.781 → 0.195 µM,
/// each well made up to a fixed final volume (600 µL, or 800 µL in an eppendorf for oils) with transfer = finalVol
/// / factor and diluent = finalVol − transfer.
/// </remarks>
public sealed class SerialDilutionScheme : ValueObject
{
    private readonly List<SerialDilutionStep> _steps;

    internal SerialDilutionScheme(
        decimal factor,
        decimal finalVolumeMicrolitres,
        IReadOnlyList<SerialDilutionStep> steps)
    {
        Factor = factor;
        FinalVolumeMicrolitres = finalVolumeMicrolitres;
        _steps = [.. steps];
    }

    /// <summary>The dilution factor between consecutive points (e.g. 4).</summary>
    public decimal Factor { get; }

    /// <summary>The fixed final volume made up in each point's well (µL).</summary>
    public decimal FinalVolumeMicrolitres { get; }

    /// <summary>The ordered points, from the top concentration down.</summary>
    public IReadOnlyList<SerialDilutionStep> Steps => _steps.AsReadOnly();

    /// <summary>The µM concentrations of the series, top down — convenient for populating plate wells.</summary>
    public IReadOnlyList<decimal> Concentrations => _steps.Select(step => step.ConcentrationMicromolar).ToList();

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Factor;
        yield return FinalVolumeMicrolitres;
        foreach (SerialDilutionStep step in _steps)
            yield return step;
    }
}
