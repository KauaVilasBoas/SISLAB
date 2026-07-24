namespace SISLAB.Modules.Experiments.Domain.Scheduling;

/// <summary>
/// A timepoint of the schedule paired with the day offset it is read at (SISLAB-10): the label comes from the
/// experimental model (e.g. "basal", "7 dias", "28° dia") and the offset is the number of days from the schedule's
/// start it falls on. Kept as an explicit pair — rather than parsed out of the label — so the day a readout happens
/// stays a cadence input driven by the model/protocol, never inferred from lab-specific label text in code.
/// </summary>
/// <param name="Label">The timepoint label from the model, used verbatim in the entry title.</param>
/// <param name="DayOffset">Days from the schedule start this timepoint is read at (0 = the start day).</param>
public sealed record ScheduledTimepoint(string Label, int DayOffset);
