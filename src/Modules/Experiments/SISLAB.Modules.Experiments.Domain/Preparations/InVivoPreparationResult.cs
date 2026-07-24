using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Experiments.Domain.Preparations;

/// <summary>
/// The immutable result of an in vivo solution preparation (SISLAB-01): the numbers the operator pipettes —
/// how much compound to weigh, how much of it (by volume) to draw when it is a liquid, the final solution volume
/// and how much diluent to add. A value object so a confirmed preparation is reproducible and never silently
/// recomputed.
/// </summary>
/// <remarks>
/// Reproduces the spreadsheet by hand: 3 g/kg on a 189.6 g group with density 0.9865 g/mL gives
/// <see cref="CompoundMassGrams"/> 0.5688 g, <see cref="CompoundVolumeMicrolitres"/> 576.58 µL of OGV,
/// <see cref="FinalVolumeMicrolitres"/> 945 µL and <see cref="DiluentVolumeMicrolitres"/> 368.42 µL of soy oil.
/// </remarks>
public sealed class InVivoPreparationResult : ValueObject
{
    internal InVivoPreparationResult(
        decimal compoundMassGrams,
        decimal? compoundVolumeMicrolitres,
        decimal finalVolumeMicrolitres,
        decimal diluentVolumeMicrolitres)
    {
        CompoundMassGrams = compoundMassGrams;
        CompoundVolumeMicrolitres = compoundVolumeMicrolitres;
        FinalVolumeMicrolitres = finalVolumeMicrolitres;
        DiluentVolumeMicrolitres = diluentVolumeMicrolitres;
    }

    /// <summary>Mass of compound to weigh (g). Zero for a vehicle-only preparation.</summary>
    public decimal CompoundMassGrams { get; }

    /// <summary>Volume of the (liquid) compound to draw (µL); null for a powder / vehicle (no volume displaced).</summary>
    public decimal? CompoundVolumeMicrolitres { get; }

    /// <summary>Total volume of the administered solution (µL), from the g:µL relation.</summary>
    public decimal FinalVolumeMicrolitres { get; }

    /// <summary>Volume of diluent (vehicle) to add (µL): final minus the liquid compound volume, else the whole final.</summary>
    public decimal DiluentVolumeMicrolitres { get; }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CompoundMassGrams;
        yield return CompoundVolumeMicrolitres;
        yield return FinalVolumeMicrolitres;
        yield return DiluentVolumeMicrolitres;
    }
}
