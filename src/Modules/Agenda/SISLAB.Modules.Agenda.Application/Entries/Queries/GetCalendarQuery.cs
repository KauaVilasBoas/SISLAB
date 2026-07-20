using System.Data;
using System.Text.Json;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Agenda.Application.Entries.Recurrence;
using SISLAB.Modules.Agenda.Domain.Entries;
using SISLAB.Modules.Experiments.Contracts;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Agenda.Application.Entries.Queries;

/// <summary>
/// Returns every calendar occurrence in the inclusive date range (card [E10.4] #4): one-off entries plus each
/// expanded instance of a recurring series (RRULE honoured, EXDATE excluded). Tenant-scoped. An experiment-linked
/// occurrence carries the experiment's <see cref="CalendarItem.ExperimentName"/>, resolved through
/// <c>Experiments.Contracts</c> — never a cross-schema JOIN.
/// </summary>
public sealed record GetCalendarQuery(DateOnly Start, DateOnly End, CalendarFilters Filters)
    : IQuery<IReadOnlyList<CalendarItem>>;

/// <summary>A single materialised calendar occurrence for the front-end calendar/agenda views.</summary>
public sealed record CalendarItem(
    Guid Id,
    string Title,
    AgendaActivityType ActivityType,
    Guid? ExperimentId,
    string? ExperimentName,
    Guid? RoomId,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    bool IsAllDay,
    bool IsRecurring,
    string? RecurrenceRule,
    DateOnly OccurrenceDate,
    Guid ResponsibleId);

internal sealed class GetCalendarQueryHandler
    : BaseDataAccess, IQueryHandler<GetCalendarQuery, IReadOnlyList<CalendarItem>>
{
    // Fetch the candidate rows for the window with the tenant filter and the cheap scalar filters pushed to
    // SQL. The recurrence window is open-ended on the left (a series that started before the window may still
    // have occurrences inside it), so a recurring row qualifies whenever its series could reach the window;
    // the in-memory expansion then keeps only the instances that actually fall in it.
    private const string Sql =
        """
        SELECT
            e.id                AS id,
            e.title             AS title,
            e.description       AS description,
            e.start_date_utc    AS startdateutc,
            e.end_date_utc      AS enddateutc,
            e.is_all_day        AS isallday,
            e.activity_type     AS activitytype,
            e.experiment_id     AS experimentid,
            e.room_id           AS roomid,
            e.recurrence_rule   AS recurrencerule,
            e.responsible_id    AS responsibleid,
            e.excluded_dates    AS excludeddatesjson
        FROM agenda.agenda_entries e
        WHERE e.company_id = @CompanyId
          AND (
                (e.recurrence_rule IS NULL AND e.start_date_utc <= @WindowEnd AND e.end_date_utc >= @WindowStart)
             OR (e.recurrence_rule IS NOT NULL AND e.start_date_utc <= @WindowEnd)
              )
          AND (@ActivityType   IS NULL OR e.activity_type  = @ActivityType)
          AND (@ResponsibleId  IS NULL OR e.responsible_id = @ResponsibleId)
          AND (@ExperimentId   IS NULL OR e.experiment_id  = @ExperimentId);
        """;

    private readonly ITenantContext _tenantContext;
    private readonly RecurrenceExpander _expander;
    private readonly IExperimentDirectory _experimentDirectory;

    public GetCalendarQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext,
        RecurrenceExpander expander,
        IExperimentDirectory experimentDirectory)
        : base(connectionFactory)
    {
        _tenantContext = tenantContext;
        _expander = expander;
        _experimentDirectory = experimentDirectory;
    }

    public async Task<IReadOnlyList<CalendarItem>> HandleAsync(
        GetCalendarQuery request,
        CancellationToken cancellationToken = default)
    {
        DateTime windowStart = request.Start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime windowEnd = request.End.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        Guid? responsibleFilter = request.Filters.OnlyMine
            ? request.Filters.CurrentUserId
            : request.Filters.ResponsibleId;

        using IDbConnection connection = await OpenConnectionAsync();
        IEnumerable<CalendarRow> rows = await connection.QueryAsync<CalendarRow>(
            new CommandDefinition(
                Sql,
                new
                {
                    CompanyId = _tenantContext.CompanyId,
                    WindowStart = windowStart,
                    WindowEnd = windowEnd,
                    ActivityType = request.Filters.ActivityType?.ToString(),
                    ResponsibleId = responsibleFilter,
                    ExperimentId = request.Filters.ExperimentId,
                },
                cancellationToken: cancellationToken));

        List<(CalendarRow Row, EntryOccurrence Occurrence)> expanded = ExpandRows(rows, windowStart, windowEnd);

        IReadOnlyDictionary<Guid, string> experimentNames = await ResolveExperimentNamesAsync(expanded, cancellationToken);

        return expanded
            .Select(pair => ToItem(pair.Row, pair.Occurrence, experimentNames))
            .OrderBy(item => item.StartDateUtc)
            .ToList();
    }

    private List<(CalendarRow, EntryOccurrence)> ExpandRows(
        IEnumerable<CalendarRow> rows, DateTime windowStart, DateTime windowEnd)
    {
        var expanded = new List<(CalendarRow, EntryOccurrence)>();

        foreach (CalendarRow row in rows)
        {
            IReadOnlyList<DateOnly> excluded = DeserializeExcludedDates(row.ExcludedDatesJson);

            IReadOnlyList<EntryOccurrence> occurrences = _expander.Expand(
                row.StartDateUtc, row.EndDateUtc, row.RecurrenceRule, excluded, windowStart, windowEnd);

            foreach (EntryOccurrence occurrence in occurrences)
                expanded.Add((row, occurrence));
        }

        return expanded;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveExperimentNamesAsync(
        List<(CalendarRow Row, EntryOccurrence Occurrence)> expanded,
        CancellationToken cancellationToken)
    {
        Guid[] experimentIds = expanded
            .Where(pair => pair.Row.ExperimentId is not null)
            .Select(pair => pair.Row.ExperimentId!.Value)
            .Distinct()
            .ToArray();

        return experimentIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _experimentDirectory.GetTitlesAsync(experimentIds, cancellationToken);
    }

    private static CalendarItem ToItem(
        CalendarRow row, EntryOccurrence occurrence, IReadOnlyDictionary<Guid, string> experimentNames)
    {
        string? experimentName = row.ExperimentId is { } id && experimentNames.TryGetValue(id, out string? name)
            ? name
            : null;

        return new CalendarItem(
            Id: row.Id,
            Title: row.Title,
            ActivityType: Enum.Parse<AgendaActivityType>(row.ActivityType),
            ExperimentId: row.ExperimentId,
            ExperimentName: experimentName,
            RoomId: row.RoomId,
            StartDateUtc: occurrence.StartUtc,
            EndDateUtc: occurrence.EndUtc,
            IsAllDay: row.IsAllDay,
            IsRecurring: row.RecurrenceRule is not null,
            // Surface the raw RRULE so the edit form can pre-populate the recurrence editor (card [E10.6]).
            RecurrenceRule: row.RecurrenceRule,
            OccurrenceDate: occurrence.OccurrenceDate,
            ResponsibleId: row.ResponsibleId);
    }

    private static IReadOnlyList<DateOnly> DeserializeExcludedDates(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<DateOnly>>(json) ?? [];

    /// <summary>Flat Dapper projection of an entry row before recurrence expansion.</summary>
    private sealed record CalendarRow(
        Guid Id,
        string Title,
        string? Description,
        DateTime StartDateUtc,
        DateTime EndDateUtc,
        bool IsAllDay,
        string ActivityType,
        Guid? ExperimentId,
        Guid? RoomId,
        string? RecurrenceRule,
        Guid ResponsibleId,
        string? ExcludedDatesJson);
}
