using SISLAB.Modules.Agenda.Application.Entries.Commands;
using SISLAB.Modules.Agenda.Contracts;
using SISLAB.Modules.Agenda.Domain.Entries;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Agenda.Application.PublicApi;

/// <summary>
/// Adapter implementing the Agenda module's public boundary <see cref="IAgendaScheduler"/> (SISLAB-10). It is the
/// single place that translates the primitives-only <see cref="ScheduleAgendaEntryRequest"/> other modules build
/// into the module's own <see cref="CreateAgendaEntryCommand"/> and dispatches it through the mediator — so a
/// programmatically created entry goes through the exact same validated write path (recurrence/reminder handling,
/// advisory conflict check, Outbox) as one created through the HTTP API. Mirrors the Configuration
/// <c>LabConfiguration</c> and Notifications <c>NotificationPublisher</c> adapters.
/// </summary>
/// <remarks>
/// <b>Delegation, not re-implementation.</b> There is no aggregate handling here: the adapter maps each request and
/// re-uses the command handler. Tenancy is resolved by that handler from <c>ITenantContext</c>, so a caller cannot
/// target another tenant through this surface.
/// </remarks>
internal sealed class AgendaScheduler : IAgendaScheduler
{
    private readonly IMediator _mediator;

    public AgendaScheduler(IMediator mediator) => _mediator = mediator;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ScheduleAsync(
        IReadOnlyList<ScheduleAgendaEntryRequest> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var createdIds = new List<Guid>(requests.Count);

        foreach (ScheduleAgendaEntryRequest request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AgendaEntryMutationResult result =
                await _mediator.SendAsync(ToCommand(request), cancellationToken);

            createdIds.Add(result.EntryId);
        }

        return createdIds;
    }

    /// <summary>
    /// Maps a public request into the module's create command. A supplied <see cref="ScheduleAgendaEntryRequest.ReminderMinutesBefore"/>
    /// becomes a single in-app reminder on the entry, so the existing reminder job delivers the véspera notification
    /// to the responsible; no reminder is attached when it is <see langword="null"/>.
    /// </summary>
    private static CreateAgendaEntryCommand ToCommand(ScheduleAgendaEntryRequest request)
    {
        IReadOnlyList<ReminderInput>? reminders = request.ReminderMinutesBefore is { } minutesBefore
            ? new[] { new ReminderInput(minutesBefore, ReminderNotificationType.InApp) }
            : null;

        return new CreateAgendaEntryCommand(
            request.Title,
            request.Description,
            request.StartUtc,
            request.EndUtc,
            request.IsAllDay,
            MapKind(request.Kind),
            request.ExperimentId,
            RoomId: null,
            RecurrenceRule: null,
            request.ResponsibleId,
            reminders,
            Color: null);
    }

    private static AgendaActivityType MapKind(ScheduledActivityKind kind) => kind switch
    {
        ScheduledActivityKind.RoomBooking => AgendaActivityType.RoomBooking,
        ScheduledActivityKind.Experiment => AgendaActivityType.Experiment,
        ScheduledActivityKind.Bioterium => AgendaActivityType.Bioterium,
        ScheduledActivityKind.Presentation => AgendaActivityType.Presentation,
        ScheduledActivityKind.Other => AgendaActivityType.Other,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown scheduled activity kind."),
    };
}
