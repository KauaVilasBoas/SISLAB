using SISLAB.Modules.Experiments.Domain.Scheduling;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Domain.Scheduling;

/// <summary>
/// Covers <see cref="ExperimentScheduleGenerator"/> (SISLAB-10): the date generation from a protocol and the roster
/// assignment over the generated days. The cadence (induction count/spacing, treatment/timepoint offsets) is entirely
/// input, mirroring the ND model of the in vivo spreadsheet — nothing lab-specific is fixed in the generator.
/// </summary>
public sealed class ExperimentScheduleGeneratorTests
{
    private static readonly Guid Vic = Guid.NewGuid();
    private static readonly Guid Dai = Guid.NewGuid();
    private static readonly DateOnly Start = new(2026, 1, 5);
    private readonly ExperimentScheduleGenerator _generator = new();

    [Fact]
    public void Generates_induction_days_from_the_protocol_cadence()
    {
        // Two inductions spaced 3 days apart (the spreadsheet's 1st/2nd induction), no treatment/timepoints.
        IReadOnlyList<ScheduledActivity> schedule = _generator.Generate(
            Start,
            administrations: 2,
            intervalDays: 3,
            treatmentDayOffsets: Array.Empty<int>(),
            timepoints: Array.Empty<ScheduledTimepoint>(),
            roster: ResponsibleRoster.Of(new[] { Vic }));

        Assert.Equal(2, schedule.Count);
        Assert.All(schedule, activity => Assert.Equal(ScheduleActivityKind.Induction, activity.Kind));
        Assert.Equal(Start, schedule[0].Date);                 // 1st induction on the start day
        Assert.Equal(Start.AddDays(3), schedule[1].Date);      // 2nd induction 3 days later
        Assert.Equal("1ª indução", schedule[0].Label);
        Assert.Equal("2ª indução", schedule[1].Label);
    }

    [Fact]
    public void Anchors_timepoints_at_their_model_day_offsets()
    {
        // The ND model's 28th-day reference readout plus a basal at day 0, over a single induction.
        var timepoints = new[]
        {
            new ScheduledTimepoint("basal", 0),
            new ScheduledTimepoint("28° dia", 28),
        };

        IReadOnlyList<ScheduledActivity> schedule = _generator.Generate(
            Start,
            administrations: 1,
            intervalDays: 0,
            treatmentDayOffsets: Array.Empty<int>(),
            timepoints: timepoints,
            roster: ResponsibleRoster.Of(new[] { Vic }));

        ScheduledActivity referenceReadout = Assert.Single(schedule, a => a.Label == "28° dia");
        Assert.Equal(ScheduleActivityKind.Timepoint, referenceReadout.Kind);
        Assert.Equal(Start.AddDays(28), referenceReadout.Date);
    }

    [Fact]
    public void Emits_activities_in_chronological_order_with_induction_before_treatment_and_timepoint()
    {
        var timepoints = new[] { new ScheduledTimepoint("basal", 0) };

        IReadOnlyList<ScheduledActivity> schedule = _generator.Generate(
            Start,
            administrations: 1,
            intervalDays: 0,
            treatmentDayOffsets: new[] { 2 },
            timepoints: timepoints,
            roster: ResponsibleRoster.Of(new[] { Vic }));

        // Day 0 carries an induction and a basal timepoint (induction first), day 2 the treatment.
        Assert.Collection(schedule,
            first => Assert.Equal(ScheduleActivityKind.Induction, first.Kind),
            second => Assert.Equal(ScheduleActivityKind.Timepoint, second.Kind),
            third =>
            {
                Assert.Equal(ScheduleActivityKind.Treatment, third.Kind);
                Assert.Equal(Start.AddDays(2), third.Date);
            });
    }

    [Fact]
    public void Rotates_the_roster_per_day_so_one_person_owns_a_whole_day()
    {
        // Days 0, 1, 2 — Vic/Dai alternation; the two activities that share day 0 get the same responsible.
        var timepoints = new[] { new ScheduledTimepoint("basal", 0) };

        IReadOnlyList<ScheduledActivity> schedule = _generator.Generate(
            Start,
            administrations: 1,
            intervalDays: 0,
            treatmentDayOffsets: new[] { 1, 2 },
            timepoints: timepoints,
            roster: ResponsibleRoster.Of(new[] { Vic, Dai }));

        // Day 0: induction + basal, both Vic. Day 1: treatment, Dai. Day 2: treatment, Vic.
        Assert.Equal(Vic, schedule.Single(a => a.Kind == ScheduleActivityKind.Induction).ResponsibleId);
        Assert.Equal(Vic, schedule.Single(a => a.Kind == ScheduleActivityKind.Timepoint).ResponsibleId);

        List<ScheduledActivity> treatments = schedule
            .Where(a => a.Kind == ScheduleActivityKind.Treatment)
            .OrderBy(a => a.Date)
            .ToList();
        Assert.Equal(Dai, treatments[0].ResponsibleId);   // day 1
        Assert.Equal(Vic, treatments[1].ResponsibleId);   // day 2
    }

    [Fact]
    public void A_single_administration_protocol_labels_the_induction_without_an_ordinal()
    {
        IReadOnlyList<ScheduledActivity> schedule = _generator.Generate(
            Start,
            administrations: 1,
            intervalDays: 0,
            treatmentDayOffsets: Array.Empty<int>(),
            timepoints: Array.Empty<ScheduledTimepoint>(),
            roster: ResponsibleRoster.Of(new[] { Vic }));

        Assert.Equal("Indução", Assert.Single(schedule).Label);
    }

    [Fact]
    public void A_negative_offset_is_rejected()
        => Assert.Throws<DomainException>(() => _generator.Generate(
            Start, 1, 0, new[] { -1 }, Array.Empty<ScheduledTimepoint>(), ResponsibleRoster.Of(new[] { Vic })));
}
