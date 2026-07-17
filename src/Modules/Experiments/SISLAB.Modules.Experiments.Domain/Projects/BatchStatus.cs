namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// Lifecycle state of a <see cref="Batch"/> (a "leva" — a cohort of animals run together, card [E11] #73). A
/// batch is planned while its design is set, becomes <see cref="Running"/> when its animals enter the study,
/// and is <see cref="Completed"/> when its schedule finishes. The batch fixes the design version it runs, so a
/// later change to the project design never rewrites a running or completed batch.
/// </summary>
public enum BatchStatus
{
    /// <summary>Planned — the batch design (groups, animals) is being set. Initial state.</summary>
    Planned = 0,

    /// <summary>Running — the batch's animals are in the study; its design is frozen.</summary>
    Running = 1,

    /// <summary>Completed — the batch's schedule has finished.</summary>
    Completed = 2,
}
