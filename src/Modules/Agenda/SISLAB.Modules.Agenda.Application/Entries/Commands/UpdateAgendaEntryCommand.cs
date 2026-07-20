using SISLAB.Modules.Agenda.Application.Entries.Conflicts;
using SISLAB.Modules.Agenda.Domain.Entries;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Agenda.Application.Entries.Commands;

/// <summary>
/// Updates a calendar entry with Google-Calendar edit semantics (card [E10.2] #2). The chosen
/// <see cref="EditScope"/> determines how the change is applied to a recurring series; for a one-off entry
/// every scope is a plain in-place update. The handler returns the id of the entry the caller should now show
/// (the original for an in-place edit, the new detached/split entry otherwise).
/// </summary>
/// <param name="EntryId">The series (or one-off) being edited.</param>
/// <param name="OccurrenceDate">
/// The date of the occurrence the user opened. Required for <see cref="EditScope.OnlyThis"/> and
/// <see cref="EditScope.ThisAndFollowing"/> on a recurring series; ignored for a one-off / all-occurrences edit.
/// </param>
public sealed record UpdateAgendaEntryCommand(
    Guid EntryId,
    EditScope Scope,
    DateOnly? OccurrenceDate,
    string Title,
    string? Description,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    bool IsAllDay,
    AgendaActivityType ActivityType,
    Guid? ExperimentId,
    Guid? RoomId,
    string? RecurrenceRule,
    IReadOnlyList<ReminderInput>? Reminders = null) : ICommand<AgendaEntryMutationResult>;

internal sealed class UpdateAgendaEntryCommandHandler
    : ICommandHandler<UpdateAgendaEntryCommand, AgendaEntryMutationResult>
{
    private readonly IAgendaEntryRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IAgendaConflictChecker _conflictChecker;

    public UpdateAgendaEntryCommandHandler(
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
        UpdateAgendaEntryCommand command, CancellationToken cancellationToken = default)
    {
        AgendaEntry entry = await _repository.GetByIdAsync(command.EntryId, cancellationToken)
            ?? throw new NotFoundException($"Agenda entry {command.EntryId} not found.");

        RecurrenceRuleSpec? recurrence = RecurrenceRuleSpec.CreateOptional(command.RecurrenceRule);

        Guid resultId = ApplyEdit(entry, command, recurrence);

        // Advisory only — report overlaps of the edited schedule but never block the update (card [E10.9] #6).
        // Exclude the entry we just touched so a series never conflicts with itself.
        IReadOnlyList<string> warnings = await _conflictChecker.CheckAsync(
            entry.ResponsibleId, command.ActivityType, command.StartDateUtc, command.EndDateUtc,
            recurrence?.Value, excludedDates: [], excludeEntryId: resultId, cancellationToken);

        return new AgendaEntryMutationResult(resultId, warnings);
    }

    private Guid ApplyEdit(AgendaEntry entry, UpdateAgendaEntryCommand command, RecurrenceRuleSpec? recurrence)
    {
        // A one-off entry has no series to split — collapse every scope to a direct in-place edit.
        if (!entry.IsRecurring || command.Scope == EditScope.AllOccurrences)
        {
            entry.Reschedule(
                command.Title, command.Description, command.StartDateUtc, command.EndDateUtc,
                command.IsAllDay, command.ActivityType, command.ExperimentId, command.RoomId, recurrence);

            if (command.Reminders is not null)
                entry.SetReminders(command.Reminders.Select(CreateAgendaEntryCommandHandler.ToReminder));

            return entry.Id;
        }

        DateOnly occurrenceDate = command.OccurrenceDate
            ?? throw new BusinessException(
                "An occurrence date is required to edit a single occurrence of a recurring entry.");

        return command.Scope switch
        {
            EditScope.OnlyThis => ExcludeAndDetach(entry, occurrenceDate, recurrence, command),
            EditScope.ThisAndFollowing => TruncateAndFork(entry, occurrenceDate, recurrence, command),
            _ => entry.Id,
        };
    }

    /// <summary>
    /// "Only this": suppress the edited occurrence in the original series and create a detached one-off carrying
    /// the new values (no recurrence — a single moved instance).
    /// </summary>
    private Guid ExcludeAndDetach(
        AgendaEntry original,
        DateOnly occurrenceDate,
        RecurrenceRuleSpec? _,
        UpdateAgendaEntryCommand command)
    {
        original.CancelOccurrence(occurrenceDate);

        AgendaEntry detached = AgendaEntry.Create(
            _tenantContext.CompanyId,
            command.Title, command.Description, command.StartDateUtc, command.EndDateUtc,
            command.IsAllDay, command.ActivityType, command.ExperimentId, command.RoomId,
            recurrenceRule: null,
            original.ResponsibleId,
            _clock.UtcNow);

        _repository.Add(detached);
        return detached.Id;
    }

    /// <summary>
    /// "This and following": truncate the original series to end before the split, and create a fresh series
    /// from the split date carrying the new values and the (possibly changed) recurrence rule.
    /// </summary>
    private Guid TruncateAndFork(
        AgendaEntry original,
        DateOnly occurrenceDate,
        RecurrenceRuleSpec? recurrence,
        UpdateAgendaEntryCommand command)
    {
        // The split instant is the edited occurrence's day at the new start's time-of-day.
        var splitStart = occurrenceDate.ToDateTime(TimeOnly.FromDateTime(command.StartDateUtc), DateTimeKind.Utc);
        original.TruncateAt(splitStart);

        AgendaEntry newSeries = AgendaEntry.Create(
            _tenantContext.CompanyId,
            command.Title, command.Description, command.StartDateUtc, command.EndDateUtc,
            command.IsAllDay, command.ActivityType, command.ExperimentId, command.RoomId,
            recurrence ?? original.RecurrenceRule,
            original.ResponsibleId,
            _clock.UtcNow);

        _repository.Add(newSeries);
        return newSeries.Id;
    }
}
