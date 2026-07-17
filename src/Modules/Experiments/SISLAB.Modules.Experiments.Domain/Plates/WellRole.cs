namespace SISLAB.Modules.Experiments.Domain.Plates;

/// <summary>
/// The role a <see cref="Well"/> plays in a plate assay (decision card #68 — plate design carries controls /
/// curve / samples). The role drives the calculation, and its meaning depends on the assay:
/// <list type="bullet">
///   <item><b>Viability (MTT).</b> <see cref="Blank"/> is the background subtracted from every well,
///   <see cref="Control"/> wells define the 100% reference, and <see cref="Sample"/> / <see cref="CurvePoint"/>
///   wells are the ones whose viability is computed.</item>
///   <item><b>Nitric oxide (Griess).</b> <see cref="Standard"/> wells (known nitrite µM in <c>ConcentrationUm</c>)
///   build the calibration curve, <see cref="Blank"/> is the baseline, and each <see cref="Sample"/> well's NO
///   concentration is read off the fitted line.</item>
/// </list>
/// </summary>
public enum WellRole
{
    /// <summary>Untreated control — its mean (minus blank) is the 100% viability reference (viability assay).</summary>
    Control = 0,

    /// <summary>Blank / background — medium only; its absorbance is subtracted / used as baseline.</summary>
    Blank = 1,

    /// <summary>A point on the concentration-response curve (a sample at a defined concentration).</summary>
    CurvePoint = 2,

    /// <summary>A treated sample well whose result (viability % or NO µM) is computed.</summary>
    Sample = 3,

    /// <summary>A calibration-curve point of known nitrite concentration (µM in <c>ConcentrationUm</c>), Griess assay.</summary>
    Standard = 4,
}
