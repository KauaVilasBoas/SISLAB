using System.Data;
using System.Text.Json;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Agenda.Application.Entries.Recurrence;
using SISLAB.Modules.Agenda.Domain.Entries;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Agenda.Application.Entries.Conflicts;

/// <summary>The kinds of scheduling conflict the calendar can warn about (card [E10.9] #6).</summary>
public static class AgendaConflictWarnings
{
    /// <summary>The responsible person already has an overlapping entry.</summary>
    public const string Person = "conflict_person";

    /// <summary>Another room booking overlaps (rooms are a shared resource; RoomBooking entries compete).</summary>
    public const string Room = "conflict_room";
}

/// <summary>
/// Advisory scheduling-conflict detection for a proposed calendar entry (card [E10.9] #6). Behind an interface
/// so the write handlers depend on the abstraction (and can be unit-tested with a stub), while the concrete
/// <see cref="AgendaConflictChecker"/> owns the Dapper read.
/// </summary>
public interface IAgendaConflictChecker
{
    /// <summary>
    /// Returns the distinct advisory conflict warnings for the proposed entry, or an empty set when clear.
    /// </summary>
    Task<IReadOnlyList<string>> CheckAsync(
        Guid responsibleId,
        AgendaActivityType activityType,
        DateTime startUtc,
        DateTime endUtc,
        string? recurrenceRule,
        IReadOnlyCollection<DateOnly> excludedDates,
        Guid? excludeEntryId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Detects scheduling conflicts for a proposed calendar entry against the active company's existing entries
/// (card [E10.9] #6). It is advisory: overlaps are reported as warnings, never blocked — consistent with the
/// Booking module's project decision that a shared-room lab needs alerts, not hard blocks.
/// </summary>
/// <remarks>
/// <para>
/// Both the proposed entry and the candidates are expanded through the shared <see cref="RecurrenceExpander"/>
/// (RRULE + EXDATE) within the proposed entry's occurrence window, so a recurring proposal is checked instance
/// by instance. Two intervals overlap when each starts before the other ends. A <b>person</b> conflict is any
/// overlap with an entry owned by the same responsible; a <b>room</b> conflict is any overlap between two
/// <see cref="AgendaActivityType.RoomBooking"/> entries (rooms are a shared resource in this model).
/// </para>
/// </remarks>
public sealed class AgendaConflictChecker : IAgendaConflictChecker
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly ITenantContext _tenantContext;
    private readonly RecurrenceExpander _expander;

    // Candidates: other entries of the company that could overlap. Exclude the entry being edited by id so a
    // series does not conflict with itself. Cheap scalar narrowing (same responsible OR both room bookings) is
    // pushed to SQL; precise interval overlap is decided after expansion.
    private const string Sql =
        """
        SELECT
            e.id              AS entryid,
            e.responsible_id  AS responsibleid,
            e.activity_type   AS activitytype,
            e.start_date_utc  AS startdateutc,
            e.end_date_utc    AS enddateutc,
            e.recurrence_rule AS recurrencerule,
            e.excluded_dates  AS excludeddatesjson
        FROM agenda.agenda_entries e
        WHERE e.company_id = @CompanyId
          AND (@ExcludeEntryId IS NULL OR e.id <> @ExcludeEntryId)
          AND e.start_date_utc <= @WindowEnd
          AND (
                e.responsible_id = @ResponsibleId
             OR (@IsRoomBooking AND e.activity_type = 'RoomBooking')
              );
        """;

    public AgendaConflictChecker(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext,
        RecurrenceExpander expander)
    {
        _connectionFactory = connectionFactory;
        _tenantContext = tenantContext;
        _expander = expander;
    }

    /// <summary>
    /// Returns the distinct conflict warnings for the proposed entry, or an empty set when it is clear. The
    /// proposal is described by value (never loaded from the DB) so it works for both create and update, before
    /// anything is persisted. <paramref name="excludeEntryId"/> excludes the entry being edited from the check.
    /// </summary>
    public async Task<IReadOnlyList<string>> CheckAsync(
        Guid responsibleId,
        AgendaActivityType activityType,
        DateTime startUtc,
        DateTime endUtc,
        string? recurrenceRule,
        IReadOnlyCollection<DateOnly> excludedDates,
        Guid? excludeEntryId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<EntryOccurrence> proposed = _expander.Expand(
            startUtc, endUtc, recurrenceRule, excludedDates, startUtc, EndOfWindow(startUtc, endUtc, recurrenceRule));

        if (proposed.Count == 0)
            return [];

        DateTime windowEnd = proposed.Max(o => o.EndUtc);
        bool isRoomBooking = activityType == AgendaActivityType.RoomBooking;

        IReadOnlyList<ConflictCandidate> candidates = await LoadCandidatesAsync(
            responsibleId, isRoomBooking, excludeEntryId, windowEnd, cancellationToken);

        var warnings = new HashSet<string>();
        DateTime windowStart = proposed.Min(o => o.StartUtc);

        foreach (ConflictCandidate candidate in candidates)
        {
            IReadOnlyList<EntryOccurrence> candidateOccurrences = _expander.Expand(
                candidate.StartDateUtc, candidate.EndDateUtc, candidate.RecurrenceRule,
                candidate.ExcludedDates, windowStart, windowEnd);

            if (!AnyOverlap(proposed, candidateOccurrences))
                continue;

            if (candidate.ResponsibleId == responsibleId)
                warnings.Add(AgendaConflictWarnings.Person);

            if (isRoomBooking && candidate.ActivityType == AgendaActivityType.RoomBooking)
                warnings.Add(AgendaConflictWarnings.Room);
        }

        return warnings.ToList();
    }

    private async Task<IReadOnlyList<ConflictCandidate>> LoadCandidatesAsync(
        Guid responsibleId, bool isRoomBooking, Guid? excludeEntryId, DateTime windowEnd, CancellationToken ct)
    {
        using IDbConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        IEnumerable<ConflictRow> rows = await connection.QueryAsync<ConflictRow>(
            new CommandDefinition(
                Sql,
                new
                {
                    CompanyId = _tenantContext.CompanyId,
                    ResponsibleId = responsibleId,
                    IsRoomBooking = isRoomBooking,
                    ExcludeEntryId = excludeEntryId,
                    WindowEnd = windowEnd,
                },
                cancellationToken: ct));

        return rows
            .Select(row => new ConflictCandidate(
                row.ResponsibleId,
                Enum.Parse<AgendaActivityType>(row.ActivityType),
                row.StartDateUtc,
                row.EndDateUtc,
                row.RecurrenceRule,
                DeserializeExcludedDates(row.ExcludedDatesJson)))
            .ToList();
    }

    private static bool AnyOverlap(
        IReadOnlyList<EntryOccurrence> a, IReadOnlyList<EntryOccurrence> b)
        => a.Any(x => b.Any(y => x.StartUtc < y.EndUtc && y.StartUtc < x.EndUtc));

    // A one-off window is just the entry itself; a recurring proposal is bounded by a year so an unbounded rule
    // (FREQ=DAILY with no UNTIL/COUNT) still yields a finite, sensible conflict horizon.
    private static DateTime EndOfWindow(DateTime startUtc, DateTime endUtc, string? recurrenceRule)
        => string.IsNullOrWhiteSpace(recurrenceRule) ? endUtc : startUtc.AddYears(1);

    private static IReadOnlyList<DateOnly> DeserializeExcludedDates(string? json)
        => string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<DateOnly>>(json) ?? [];

    private sealed record ConflictCandidate(
        Guid ResponsibleId,
        AgendaActivityType ActivityType,
        DateTime StartDateUtc,
        DateTime EndDateUtc,
        string? RecurrenceRule,
        IReadOnlyList<DateOnly> ExcludedDates);

    private sealed record ConflictRow(
        Guid EntryId,
        Guid ResponsibleId,
        string ActivityType,
        DateTime StartDateUtc,
        DateTime EndDateUtc,
        string? RecurrenceRule,
        string? ExcludedDatesJson);
}
