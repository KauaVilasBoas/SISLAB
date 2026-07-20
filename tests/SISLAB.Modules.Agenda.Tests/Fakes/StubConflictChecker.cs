using SISLAB.Modules.Agenda.Application.Entries.Conflicts;
using SISLAB.Modules.Agenda.Domain.Entries;

namespace SISLAB.Modules.Agenda.Tests.Fakes;

/// <summary>
/// A stub <see cref="IAgendaConflictChecker"/> that returns a fixed warning set, so command-handler tests can
/// assert warning propagation without a database. Records the last call for inspection.
/// </summary>
internal sealed class StubConflictChecker : IAgendaConflictChecker
{
    private readonly IReadOnlyList<string> _warnings;

    public StubConflictChecker(params string[] warnings) => _warnings = warnings;

    public Guid? LastExcludeEntryId { get; private set; }

    public Task<IReadOnlyList<string>> CheckAsync(
        Guid responsibleId,
        AgendaActivityType activityType,
        DateTime startUtc,
        DateTime endUtc,
        string? recurrenceRule,
        IReadOnlyCollection<DateOnly> excludedDates,
        Guid? excludeEntryId,
        CancellationToken cancellationToken)
    {
        LastExcludeEntryId = excludeEntryId;
        return Task.FromResult(_warnings);
    }
}
