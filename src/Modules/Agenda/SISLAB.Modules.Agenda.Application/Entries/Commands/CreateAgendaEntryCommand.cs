using SISLAB.Modules.Agenda.Application.Entries.Conflicts;
using SISLAB.Modules.Agenda.Domain.Entries;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Agenda.Application.Entries.Commands;

/// <summary>
/// Creates a new calendar entry (card [E10.2] #2). <see cref="ResponsibleId"/> is resolved by the controller
/// from the authenticated principal and passed in — never taken from the client body — so an entry is always
/// owned by a real member of the active company (defense-in-depth, section 7). <see cref="RecurrenceRule"/> is
/// the raw RRULE string; the handler validates it through <see cref="RecurrenceRuleSpec"/>.
/// </summary>
public sealed record CreateAgendaEntryCommand(
    string Title,
    string? Description,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    bool IsAllDay,
    AgendaActivityType ActivityType,
    Guid? ExperimentId,
    Guid? RoomId,
    string? RecurrenceRule,
    Guid ResponsibleId,
    IReadOnlyList<ReminderInput>? Reminders = null) : ICommand<AgendaEntryMutationResult>;

/// <summary>A reminder to configure on the entry (card [E10.8] #5): fire this many minutes before each occurrence.</summary>
public sealed record ReminderInput(int MinutesBefore, ReminderNotificationType NotificationType);

/// <summary>
/// Result of a create/update command (card [E10.9] #6): the id of the entry to display plus any advisory
/// scheduling <see cref="Warnings"/> (e.g. <c>conflict_person</c>, <c>conflict_room</c>). Warnings never block
/// the write — the operation still succeeds (200 OK); they only surface the overlap to the operator.
/// </summary>
public sealed record AgendaEntryMutationResult(Guid EntryId, IReadOnlyList<string> Warnings);

internal sealed class CreateAgendaEntryCommandHandler
    : ICommandHandler<CreateAgendaEntryCommand, AgendaEntryMutationResult>
{
    private readonly IAgendaEntryRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IAgendaConflictChecker _conflictChecker;

    public CreateAgendaEntryCommandHandler(
        IAgendaEntryRepository repository,
        ITenantContext tenantContext,
        IClock clock,
        IAgendaConflictChecker conflictChecker)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _clock = clock;
        _conflictChecker = conflictChecker;
    }

    public async Task<AgendaEntryMutationResult> HandleAsync(
        CreateAgendaEntryCommand command, CancellationToken cancellationToken = default)
    {
        RecurrenceRuleSpec? recurrence = RecurrenceRuleSpec.CreateOptional(command.RecurrenceRule);

        AgendaEntry entry = AgendaEntry.Create(
            _tenantContext.CompanyId,
            command.Title,
            command.Description,
            command.StartDateUtc,
            command.EndDateUtc,
            command.IsAllDay,
            command.ActivityType,
            command.ExperimentId,
            command.RoomId,
            recurrence,
            command.ResponsibleId,
            _clock.UtcNow,
            command.Reminders?.Select(ToReminder));

        _repository.Add(entry);

        // Advisory only — detect overlaps but never block the create (card [E10.9] #6).
        IReadOnlyList<string> warnings = await _conflictChecker.CheckAsync(
            command.ResponsibleId, command.ActivityType, command.StartDateUtc, command.EndDateUtc,
            recurrence?.Value, entry.ExcludedDates, excludeEntryId: entry.Id, cancellationToken);

        return new AgendaEntryMutationResult(entry.Id, warnings);
    }

    internal static EntryReminder ToReminder(ReminderInput input)
        => EntryReminder.Create(input.MinutesBefore, input.NotificationType);
}
