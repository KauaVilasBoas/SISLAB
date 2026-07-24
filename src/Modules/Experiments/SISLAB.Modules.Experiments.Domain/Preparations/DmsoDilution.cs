using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Preparations;

/// <summary>
/// The DMSO (vehicle solvent) control of an in vitro plate (SISLAB-05): the spreadsheet's "DMSO 300 µL in 1500 µL".
/// It expresses the solvent fraction in the prepared mother solution and the resulting fraction once that solution
/// is applied to the well, so the operator keeps DMSO under the cytotoxic threshold. A value object.
/// </summary>
/// <remarks>
/// The <see cref="SolutionFraction"/> is DMSO ÷ solution volume (300 / 1500 = 0.20). When the mother solution is
/// added to a well that already holds an equal volume of medium (the "half in the well" layout), the DMSO is
/// diluted once more, giving the <see cref="WellFraction"/>. Volumes and the in-well dilution ratio are inputs.
/// </remarks>
public sealed class DmsoDilution : ValueObject
{
    private DmsoDilution(
        decimal dmsoMicrolitres,
        decimal solutionMicrolitres,
        decimal solutionFraction,
        decimal wellFraction)
    {
        DmsoMicrolitres = dmsoMicrolitres;
        SolutionMicrolitres = solutionMicrolitres;
        SolutionFraction = solutionFraction;
        WellFraction = wellFraction;
    }

    /// <summary>Volume of pure DMSO in the mother solution (µL).</summary>
    public decimal DmsoMicrolitres { get; }

    /// <summary>Total volume of the mother solution the DMSO is dissolved in (µL).</summary>
    public decimal SolutionMicrolitres { get; }

    /// <summary>DMSO fraction of the mother solution (e.g. 0.20 for 300 µL in 1500 µL).</summary>
    public decimal SolutionFraction { get; }

    /// <summary>DMSO fraction once the solution reaches the well, after the in-well dilution ratio.</summary>
    public decimal WellFraction { get; }

    /// <summary>
    /// Computes the DMSO fractions in the mother solution and in the well. <paramref name="inWellDilutionRatio"/>
    /// is how much the solution is further diluted in the well (1 = applied neat; 2 = half solution + half medium).
    /// </summary>
    public static DmsoDilution Of(
        decimal dmsoMicrolitres,
        decimal solutionMicrolitres,
        decimal inWellDilutionRatio = 1m)
    {
        Guard.AgainstNonPositive(dmsoMicrolitres, nameof(dmsoMicrolitres));
        Guard.AgainstNonPositive(solutionMicrolitres, nameof(solutionMicrolitres));
        Guard.AgainstNonPositive(inWellDilutionRatio, nameof(inWellDilutionRatio));

        decimal solutionFraction = dmsoMicrolitres / solutionMicrolitres;
        decimal wellFraction = solutionFraction / inWellDilutionRatio;

        return new DmsoDilution(
            dmsoMicrolitres,
            solutionMicrolitres,
            Math.Round(solutionFraction, 6),
            Math.Round(wellFraction, 6));
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return DmsoMicrolitres;
        yield return SolutionMicrolitres;
        yield return SolutionFraction;
        yield return WellFraction;
    }
}
