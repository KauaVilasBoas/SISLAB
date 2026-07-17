namespace SISLAB.Modules.Experiments.Domain.Experiments;

/// <summary>
/// The role a <see cref="ExperimentStep"/> plays in an experiment's execution flow (decision card #68 —
/// "Steps as a first-class citizen"). Each experiment type defines its default sequence of step kinds via
/// its protocol; the concrete experiment executes them, recording who performed each and when.
/// </summary>
/// <remarks>
/// The full set from the discovery is modelled so later assays reuse it without an enum change. The in
/// vitro viability flow in this slice uses <see cref="Baseline"/> (plate design), <see cref="Measurement"/>
/// (reader import), <see cref="Calculation"/> (% viability) and <see cref="Analysis"/> (export-ready).
/// </remarks>
public enum ExperimentStepKind
{
    /// <summary>Baseline / initial setup (e.g. designing the plate, pre-dose reference reads).</summary>
    Baseline = 0,

    /// <summary>Administering a dose to a subject (in vivo).</summary>
    DoseAdministration = 1,

    /// <summary>A scheduled timepoint at which a measurement is taken (in vivo).</summary>
    Timepoint = 2,

    /// <summary>Taking a measurement / importing raw instrument data (e.g. plate reader absorbance).</summary>
    Measurement = 3,

    /// <summary>Collecting a sample / tissue into the biobank (in vivo).</summary>
    Collection = 4,

    /// <summary>Running the versioned calculation over the measurements to produce a result snapshot.</summary>
    Calculation = 5,

    /// <summary>Analysing / exporting the computed dataset (e.g. for GraphPad Prism).</summary>
    Analysis = 6,
}
