namespace SISLAB.Modules.Experiments.Domain.Biobank;

/// <summary>
/// Lifecycle of an <see cref="Analysis"/> run against a biobank <see cref="Sample"/> (card [E11] #89). An
/// analysis reserves (consumes) part of the sample the moment it is requested — the aliquot is spent whether or
/// not the reading is back yet — so it starts <see cref="Pending"/> and is signed off to <see cref="Completed"/>
/// once its result is recorded.
/// </summary>
public enum AnalysisStatus
{
    /// <summary>Requested and consuming the sample, but its result is not yet recorded.</summary>
    Pending = 0,

    /// <summary>Result recorded and signed off.</summary>
    Completed = 1,
}
