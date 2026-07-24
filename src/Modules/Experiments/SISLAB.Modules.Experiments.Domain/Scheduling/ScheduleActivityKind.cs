namespace SISLAB.Modules.Experiments.Domain.Scheduling;

/// <summary>
/// The kind of activity a generated schedule entry represents (SISLAB-10). Drives the entry's title and lets the
/// calendar tell an induction day apart from a treatment day or a readout (timepoint). Kept as a domain enum so the
/// generator stays independent of the Agenda module; the application layer maps it to the Agenda activity kind when
/// materialising the entries.
/// </summary>
public enum ScheduleActivityKind
{
    /// <summary>An induction administration (e.g. the 1st/2nd induction of the ND model).</summary>
    Induction = 1,

    /// <summary>A treatment/administration day between induction and the reference readout.</summary>
    Treatment = 2,

    /// <summary>A readout/measurement at one of the model's timepoints (e.g. basal, 7/15/21/28 dias).</summary>
    Timepoint = 3,
}
