namespace SISLAB.Modules.Experiments.Domain.Scheduling;

/// <summary>
/// One entry of a generated experiment schedule (SISLAB-10): the calendar day of an activity, its
/// <see cref="ScheduleActivityKind"/>, a human-readable <see cref="Label"/> and the <see cref="ResponsibleId"/> the
/// roster put on duty for that day. A pure, immutable result of
/// <see cref="ExperimentScheduleGenerator.Generate"/> — it holds no infrastructure and is what the application layer
/// translates into an Agenda entry request. The day is a <see cref="DateOnly"/> because the schedule is expressed in
/// whole days derived from the protocol's cadence, not wall-clock instants.
/// </summary>
/// <param name="Date">The calendar day the activity falls on.</param>
/// <param name="Kind">Whether the activity is an induction, a treatment day or a timepoint readout.</param>
/// <param name="Label">A short description of the activity (e.g. "1ª indução", "Tratamento — dia 3", "28 dias").</param>
/// <param name="ResponsibleId">The roster's pick for the day, by value.</param>
public sealed record ScheduledActivity(
    DateOnly Date,
    ScheduleActivityKind Kind,
    string Label,
    Guid ResponsibleId);
