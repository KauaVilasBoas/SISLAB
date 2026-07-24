using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Experiments.Domain.Preparations;

/// <summary>
/// One point of a serial dilution (SISLAB-05): the target <see cref="ConcentrationMicromolar"/> at this point, and
/// how to reach it — the <see cref="TransferMicrolitres"/> drawn from the previous, more concentrated point and the
/// <see cref="DiluentMicrolitres"/> added, together making up the fixed final volume. A value object.
/// </summary>
/// <remarks>
/// The first (top) point is the mother concentration itself: it is not diluted from a previous step, so its
/// <see cref="TransferMicrolitres"/> is null and its <see cref="DiluentMicrolitres"/> is null — it is prepared from
/// the stock, not by transfer. Every subsequent point applies <c>C1·V1 = C2·V2</c>: transfer = finalVolume / factor,
/// diluent = finalVolume − transfer.
/// </remarks>
public sealed class SerialDilutionStep : ValueObject
{
    internal SerialDilutionStep(
        int index,
        decimal concentrationMicromolar,
        decimal? transferMicrolitres,
        decimal? diluentMicrolitres,
        decimal finalVolumeMicrolitres)
    {
        Index = index;
        ConcentrationMicromolar = concentrationMicromolar;
        TransferMicrolitres = transferMicrolitres;
        DiluentMicrolitres = diluentMicrolitres;
        FinalVolumeMicrolitres = finalVolumeMicrolitres;
    }

    /// <summary>Zero-based position of the point in the series (0 = the top/mother concentration).</summary>
    public int Index { get; }

    /// <summary>Target concentration at this point (µM).</summary>
    public decimal ConcentrationMicromolar { get; }

    /// <summary>Volume drawn from the previous point (µL); null for the top point (prepared from stock).</summary>
    public decimal? TransferMicrolitres { get; }

    /// <summary>Volume of diluent added at this point (µL); null for the top point.</summary>
    public decimal? DiluentMicrolitres { get; }

    /// <summary>The fixed final volume held in each point's well (µL) — e.g. 600, or 800 in an eppendorf for oils.</summary>
    public decimal FinalVolumeMicrolitres { get; }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Index;
        yield return ConcentrationMicromolar;
        yield return TransferMicrolitres;
        yield return DiluentMicrolitres;
        yield return FinalVolumeMicrolitres;
    }
}
