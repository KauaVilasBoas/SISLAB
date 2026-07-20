using System.Data;
using System.Text.Json;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Agenda.Application.Entries.Queries;

/// <summary>
/// Returns the active company's calendar entries that have at least one configured reminder and whose series
/// could still fire within the look-ahead window (card [E10.8] #5). Used by the reminder job, which expands the
/// recurrence in-process to find each occurrence's exact start and decides which reminders are due now.
/// Tenant-scoped.
/// </summary>
/// <param name="HorizonUtc">
/// The far edge of the look-ahead: only entries whose first occurrence starts at or before this instant are
/// candidates. A recurring series (open-ended) always qualifies; a one-off qualifies only if it starts before
/// the horizon. The job applies the precise per-reminder lead-time check after expansion.
/// </param>
public sealed record GetEntriesWithRemindersQuery(DateTime HorizonUtc)
    : IQuery<IReadOnlyList<ReminderCandidate>>;

/// <summary>A configured reminder on a candidate entry (flattened for the job).</summary>
public sealed record ReminderCandidateReminder(int MinutesBefore, string NotificationType);

/// <summary>An entry that carries reminders, with everything the job needs to expand it and fire due reminders.</summary>
public sealed record ReminderCandidate(
    Guid EntryId,
    string Title,
    Guid ResponsibleId,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    string? RecurrenceRule,
    IReadOnlyList<DateOnly> ExcludedDates,
    IReadOnlyList<ReminderCandidateReminder> Reminders);

internal sealed class GetEntriesWithRemindersQueryHandler
    : BaseDataAccess, IQueryHandler<GetEntriesWithRemindersQuery, IReadOnlyList<ReminderCandidate>>
{
    private const string Sql =
        """
        SELECT
            e.id                AS entryid,
            e.title             AS title,
            e.responsible_id    AS responsibleid,
            e.start_date_utc    AS startdateutc,
            e.end_date_utc      AS enddateutc,
            e.recurrence_rule   AS recurrencerule,
            e.excluded_dates    AS excludeddatesjson,
            r.minutes_before    AS minutesbefore,
            r.notification_type AS notificationtype
        FROM agenda.agenda_entries e
        INNER JOIN agenda.entry_reminders r ON r.entry_id = e.id
        WHERE e.company_id = @CompanyId
          AND (e.recurrence_rule IS NOT NULL OR e.start_date_utc <= @HorizonUtc)
        ORDER BY e.id;
        """;

    private readonly ITenantContext _tenantContext;

    public GetEntriesWithRemindersQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory) => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<ReminderCandidate>> HandleAsync(
        GetEntriesWithRemindersQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();
        IEnumerable<ReminderRow> rows = await connection.QueryAsync<ReminderRow>(
            new CommandDefinition(
                Sql,
                new { CompanyId = _tenantContext.CompanyId, request.HorizonUtc },
                cancellationToken: cancellationToken));

        // One row per (entry × reminder); group back into a candidate per entry.
        return rows
            .GroupBy(row => row.EntryId)
            .Select(group =>
            {
                ReminderRow first = group.First();
                return new ReminderCandidate(
                    first.EntryId,
                    first.Title,
                    first.ResponsibleId,
                    first.StartDateUtc,
                    first.EndDateUtc,
                    first.RecurrenceRule,
                    DeserializeExcludedDates(first.ExcludedDatesJson),
                    group.Select(r => new ReminderCandidateReminder(r.MinutesBefore, r.NotificationType)).ToList());
            })
            .ToList();
    }

    private static IReadOnlyList<DateOnly> DeserializeExcludedDates(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<DateOnly>>(json) ?? [];

    private sealed record ReminderRow(
        Guid EntryId,
        string Title,
        Guid ResponsibleId,
        DateTime StartDateUtc,
        DateTime EndDateUtc,
        string? RecurrenceRule,
        string? ExcludedDatesJson,
        int MinutesBefore,
        string NotificationType);
}
