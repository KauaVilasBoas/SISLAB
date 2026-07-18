namespace SISLAB.Modules.Experiments.Domain.Experiments;

/// <summary>
/// Lifecycle state of an <see cref="Experiment"/> (decision card #68). The aggregate owns the transition
/// policy: an experiment is designed (<see cref="Draft"/>), executed (<see cref="InProgress"/>), sits
/// <see cref="AwaitingAnalysis"/> once the calculation produced its result snapshot (the in vitro
/// "one person generates, another calculates" hand-off), is <see cref="Completed"/> when the analysis is
/// signed off, and may be <see cref="Archived"/> at the end.
/// </summary>
public enum ExperimentStatus
{
    /// <summary>Being designed — plate layout / metadata still editable. Initial state.</summary>
    Draft = 0,

    /// <summary>Execution started — readings are being taken.</summary>
    InProgress = 1,

    /// <summary>Calculation ran and produced its snapshot; awaiting the human analysis/sign-off.</summary>
    AwaitingAnalysis = 2,

    /// <summary>Analysis complete.</summary>
    Completed = 3,

    /// <summary>Retired from the active list, kept for the record.</summary>
    Archived = 4,

    /// <summary>
    /// In vivo hand-off: the raw per-timepoint data has been launched by one operator and the experiment is
    /// waiting for the versioned calculation (e.g. von Frey up-down 50% threshold) to be run by another — the
    /// "one person generates, another calculates" flow reused from in vitro.
    /// </summary>
    AwaitingCalculation = 5,
}
