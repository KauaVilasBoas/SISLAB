using FluentValidation;
using SISLAB.Modules.Agenda.Contracts;
using SISLAB.Modules.Configuration.Contracts;
using SISLAB.Modules.Experiments.Domain.Scheduling;
using SISLAB.Modules.Identity.Contracts.Administration;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Scheduling.Commands;

/// <summary>
/// Generates an experiment's schedule from its bound experimental model / induction protocol (SISLAB-10) and
/// materialises it as calendar entries in the Agenda module, rotating a configurable roster of responsibles across
/// the days. This is the card's orchestration seam: Experiments owns the "what happens on which day" derivation and
/// delegates the "put it on the calendar" and "remind the responsible" concerns to Agenda via its Contracts port.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cadence from the model, not the code.</b> The induction count and spacing come from the model's
/// <c>InductionProtocol</c> (read through <see cref="ILabConfiguration"/>); the treatment days and the per-timepoint
/// day offsets are supplied by the caller (derived from the model), one offset per model timepoint. Nothing about the
/// current lab is hardcoded — a different model yields a different schedule.
/// </para>
/// <para>
/// <b>Configurable rotation.</b> <see cref="Responsibles"/> is the ordered rotation list and <see cref="DaysPerShift"/>
/// its cadence — two people at one day per shift reproduce the spreadsheet's day-on/day-off "Vic e Dai" alternation,
/// but neither the people nor the cadence are fixed. Every responsible is validated as an active member of the
/// company through the Identity Contracts port (module isolation, section 2).
/// </para>
/// <para>
/// <b>Véspera reminders.</b> When <see cref="ReminderMinutesBefore"/> is set, each created entry carries that reminder
/// lead time, so the Agenda module's existing reminder job delivers the day-before notification to the responsible —
/// the reminder integration rides on Agenda's own mechanism rather than a separate Notifications call.
/// </para>
/// </remarks>
/// <param name="ExperimentId">The experiment the generated entries link to, by value.</param>
/// <param name="ExperimentalModelId">The model whose induction protocol drives the cadence (validated + read via Configuration).</param>
/// <param name="StartDate">Day 0 of the schedule (the first induction day).</param>
/// <param name="TreatmentDayOffsets">Day offsets (from the start) of treatment days, derived from the model.</param>
/// <param name="TimepointDayOffsets">One day offset per model timepoint, in the model's timepoint order.</param>
/// <param name="Responsibles">The ordered rotation list of responsible member ids.</param>
/// <param name="DaysPerShift">How many consecutive days one responsible covers before rotation (≥ 1).</param>
/// <param name="ReminderMinutesBefore">Optional véspera-reminder lead time in minutes; <see langword="null"/> for none.</param>
public sealed record GenerateExperimentScheduleCommand(
    Guid ExperimentId,
    Guid ExperimentalModelId,
    DateOnly StartDate,
    IReadOnlyList<int> TreatmentDayOffsets,
    IReadOnlyList<int> TimepointDayOffsets,
    IReadOnlyList<Guid> Responsibles,
    int DaysPerShift,
    int? ReminderMinutesBefore) : ICommand<GenerateExperimentScheduleResult>;

/// <summary>Result of a schedule generation: the ids of the calendar entries created, in chronological order.</summary>
/// <param name="CreatedEntryIds">The Agenda entry ids created for the schedule, in chronological order.</param>
public sealed record GenerateExperimentScheduleResult(IReadOnlyList<Guid> CreatedEntryIds);

internal sealed class GenerateExperimentScheduleCommandValidator
    : AbstractValidator<GenerateExperimentScheduleCommand>
{
    public GenerateExperimentScheduleCommandValidator()
    {
        RuleFor(command => command.ExperimentId).NotEmpty();
        RuleFor(command => command.ExperimentalModelId).NotEmpty();
        RuleFor(command => command.Responsibles).NotEmpty();
        RuleForEach(command => command.Responsibles).NotEmpty();
        RuleFor(command => command.DaysPerShift).GreaterThanOrEqualTo(1);
        RuleForEach(command => command.TreatmentDayOffsets).GreaterThanOrEqualTo(0);
        RuleForEach(command => command.TimepointDayOffsets).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ReminderMinutesBefore)
            .GreaterThan(0)
            .When(command => command.ReminderMinutesBefore.HasValue)
            .WithMessage("A reminder lead time must be a positive number of minutes.");
    }
}

internal sealed class GenerateExperimentScheduleCommandHandler
    : ICommandHandler<GenerateExperimentScheduleCommand, GenerateExperimentScheduleResult>
{
    private readonly ILabConfiguration _labConfiguration;
    private readonly ICompanyMembershipQuery _membership;
    private readonly IAgendaScheduler _agendaScheduler;
    private readonly ITenantContext _tenantContext;
    private readonly ExperimentScheduleGenerator _generator;

    public GenerateExperimentScheduleCommandHandler(
        ILabConfiguration labConfiguration,
        ICompanyMembershipQuery membership,
        IAgendaScheduler agendaScheduler,
        ITenantContext tenantContext,
        ExperimentScheduleGenerator generator)
    {
        _labConfiguration = labConfiguration;
        _membership = membership;
        _agendaScheduler = agendaScheduler;
        _tenantContext = tenantContext;
        _generator = generator;
    }

    public async Task<GenerateExperimentScheduleResult> HandleAsync(
        GenerateExperimentScheduleCommand request, CancellationToken cancellationToken = default)
    {
        ExperimentalModelDto model =
            await _labConfiguration.GetExperimentalModelAsync(request.ExperimentalModelId, cancellationToken)
            ?? throw new BusinessException(
                $"Experimental model '{request.ExperimentalModelId}' was not found for the active company.");

        // One offset per model timepoint, in the model's order — the readout days are model-driven, not inferred.
        if (request.TimepointDayOffsets.Count != model.Timepoints.Count)
            throw new BusinessException(
                $"The schedule expects one day offset per model timepoint " +
                $"({model.Timepoints.Count}), but {request.TimepointDayOffsets.Count} were provided.");

        // Every responsible in the rotation must be an active member of the company (module isolation, section 2).
        foreach (Guid responsibleId in request.Responsibles.Distinct())
        {
            if (!await _membership.IsActiveMemberAsync(_tenantContext.CompanyId, responsibleId, cancellationToken))
                throw new BusinessException(
                    $"User '{responsibleId}' is not an active member of the company and cannot be on the roster.");
        }

        ResponsibleRoster roster = ResponsibleRoster.Of(request.Responsibles, request.DaysPerShift);

        List<ScheduledTimepoint> timepoints = model.Timepoints
            .Select((label, index) => new ScheduledTimepoint(label, request.TimepointDayOffsets[index]))
            .ToList();

        IReadOnlyList<ScheduledActivity> activities = _generator.Generate(
            request.StartDate,
            model.Induction.Administrations,
            model.Induction.IntervalDays,
            request.TreatmentDayOffsets,
            timepoints,
            roster);

        List<ScheduleAgendaEntryRequest> entryRequests = activities
            .Select(activity => ToRequest(request.ExperimentId, activity, request.ReminderMinutesBefore))
            .ToList();

        IReadOnlyList<Guid> createdIds = await _agendaScheduler.ScheduleAsync(entryRequests, cancellationToken);

        return new GenerateExperimentScheduleResult(createdIds);
    }

    // A generated activity becomes an all-day Experiment entry linked to the experiment, owned by the roster's pick
    // for the day, carrying the optional véspera reminder.
    private static ScheduleAgendaEntryRequest ToRequest(
        Guid experimentId, ScheduledActivity activity, int? reminderMinutesBefore)
    {
        var startUtc = activity.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return new ScheduleAgendaEntryRequest(
            Title: activity.Label,
            Description: null,
            StartUtc: startUtc,
            EndUtc: startUtc,
            IsAllDay: true,
            Kind: ScheduledActivityKind.Experiment,
            ExperimentId: experimentId,
            ResponsibleId: activity.ResponsibleId,
            ReminderMinutesBefore: reminderMinutesBefore);
    }
}
