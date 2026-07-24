using SISLAB.Modules.Agenda.Contracts;
using SISLAB.Modules.Configuration.Contracts;
using SISLAB.Modules.Experiments.Application.Scheduling.Commands;
using SISLAB.Modules.Experiments.Domain.Scheduling;
using SISLAB.Modules.Experiments.Tests.Fakes;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// Covers the <see cref="GenerateExperimentScheduleCommandHandler"/> (SISLAB-10): it reads the model's induction
/// cadence through the Configuration port, generates the schedule with the rotating roster and materialises it as
/// calendar entries through the Agenda port. Agenda, Configuration and Identity are all faked via their Contracts, so
/// the orchestration is exercised without any of those modules' internals.
/// </summary>
public sealed class GenerateExperimentScheduleCommandHandlerTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ModelId = Guid.NewGuid();
    private static readonly Guid ExperimentId = Guid.NewGuid();
    private static readonly Guid Vic = Guid.NewGuid();
    private static readonly Guid Dai = Guid.NewGuid();
    private static readonly DateOnly Start = new(2026, 1, 5);

    // ND-like model: two inductions 3 days apart, basal + 28-day readout.
    private static FakeLabConfiguration NdModel()
        => new FakeLabConfiguration().WithModel(
            ModelId,
            new InductionProtocolDto(Administrations: 2, IntervalDays: 3, ReferenceDayAfterInduction: 28),
            new[] { "basal", "28° dia" });

    private static GenerateExperimentScheduleCommand Command(int? reminderMinutesBefore = null)
        => new(
            ExperimentId,
            ModelId,
            Start,
            TreatmentDayOffsets: new[] { 1, 2 },
            TimepointDayOffsets: new[] { 0, 28 },      // one offset per model timepoint (basal, 28° dia)
            Responsibles: new[] { Vic, Dai },
            DaysPerShift: 1,
            ReminderMinutesBefore: reminderMinutesBefore);

    private static GenerateExperimentScheduleCommandHandler Handler(
        FakeLabConfiguration lab, FakeAgendaScheduler agenda, params Guid[] members)
        => new(
            lab,
            new FakeCompanyMembershipQuery(members),
            agenda,
            new StubTenantContext(CompanyId),
            new ExperimentScheduleGenerator());

    [Fact]
    public async Task Generates_entries_for_induction_treatment_and_timepoints_from_the_model()
    {
        var agenda = new FakeAgendaScheduler();
        GenerateExperimentScheduleResult result =
            await Handler(NdModel(), agenda, Vic, Dai).HandleAsync(Command());

        // 2 inductions + 2 treatments + 2 timepoints = 6 entries, all linked to the experiment.
        Assert.Equal(6, agenda.Requests.Count);
        Assert.Equal(6, result.CreatedEntryIds.Count);
        Assert.All(agenda.Requests, r =>
        {
            Assert.Equal(ScheduledActivityKind.Experiment, r.Kind);
            Assert.Equal(ExperimentId, r.ExperimentId);
            Assert.True(r.IsAllDay);
        });

        // The 2nd induction lands 3 days after the start — cadence taken from the model, not the code.
        Assert.Contains(agenda.Requests, r =>
            r.Title == "2ª indução" && r.StartUtc == Start.AddDays(3).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        // The 28-day readout lands at the model's timepoint offset.
        Assert.Contains(agenda.Requests, r =>
            r.Title == "28° dia" && r.StartUtc == Start.AddDays(28).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Rotates_the_responsible_across_days()
    {
        var agenda = new FakeAgendaScheduler();
        await Handler(NdModel(), agenda, Vic, Dai).HandleAsync(Command());

        // Day 0 (induction + basal) → Vic; day 1 (treatment) → Dai; day 2 (treatment) → Vic.
        DateTime day0 = Start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime day1 = Start.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime day2 = Start.AddDays(2).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        Assert.All(agenda.Requests.Where(r => r.StartUtc == day0), r => Assert.Equal(Vic, r.ResponsibleId));
        Assert.All(agenda.Requests.Where(r => r.StartUtc == day1), r => Assert.Equal(Dai, r.ResponsibleId));
        Assert.All(agenda.Requests.Where(r => r.StartUtc == day2), r => Assert.Equal(Vic, r.ResponsibleId));
    }

    [Fact]
    public async Task Attaches_the_vespera_reminder_to_each_entry_when_requested()
    {
        var agenda = new FakeAgendaScheduler();
        await Handler(NdModel(), agenda, Vic, Dai).HandleAsync(Command(reminderMinutesBefore: 1440));

        Assert.All(agenda.Requests, r => Assert.Equal(1440, r.ReminderMinutesBefore));
    }

    [Fact]
    public async Task Omits_the_reminder_when_none_is_requested()
    {
        var agenda = new FakeAgendaScheduler();
        await Handler(NdModel(), agenda, Vic, Dai).HandleAsync(Command());

        Assert.All(agenda.Requests, r => Assert.Null(r.ReminderMinutesBefore));
    }

    [Fact]
    public async Task Rejects_a_missing_model()
    {
        var agenda = new FakeAgendaScheduler();
        // Empty lab config → the model is unknown.
        var handler = Handler(new FakeLabConfiguration(), agenda, Vic, Dai);

        await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(Command()));
        Assert.Empty(agenda.Requests);
    }

    [Fact]
    public async Task Rejects_a_timepoint_offset_count_that_does_not_match_the_model()
    {
        var agenda = new FakeAgendaScheduler();
        var command = Command() with { TimepointDayOffsets = new[] { 0 } }; // model has 2 timepoints

        await Assert.ThrowsAsync<BusinessException>(() =>
            Handler(NdModel(), agenda, Vic, Dai).HandleAsync(command));
        Assert.Empty(agenda.Requests);
    }

    [Fact]
    public async Task Rejects_a_responsible_who_is_not_a_company_member()
    {
        var agenda = new FakeAgendaScheduler();
        // Only Vic is an active member; Dai is on the roster but not a member.
        var handler = Handler(NdModel(), agenda, Vic);

        await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(Command()));
        Assert.Empty(agenda.Requests);
    }
}
