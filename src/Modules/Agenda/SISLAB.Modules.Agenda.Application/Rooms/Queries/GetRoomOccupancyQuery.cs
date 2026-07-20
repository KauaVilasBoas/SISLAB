using System.Data;
using System.Text.Json;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Agenda.Application.Entries.Recurrence;
using SISLAB.Modules.Agenda.Domain.Entries;
using SISLAB.Modules.Identity.Contracts.Administration;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Agenda.Application.Rooms.Queries;

/// <summary>
/// Room-occupancy timeline for a single day (card [E10.11]), the read model behind the Gantt view. Returns every
/// <see cref="AgendaActivityType.RoomBooking"/> agenda entry that has an occurrence on <see cref="Date"/> —
/// one-off entries plus each recurring series instance whose expanded date lands on that day (RRULE honoured,
/// EXDATE excluded via the shared <see cref="RecurrenceExpander"/>). Tenant-scoped.
/// </summary>
/// <remarks>
/// The responsible person's display name is resolved through <c>Identity.Contracts</c>
/// (<see cref="ILumenUserGateway"/>) — never a cross-schema JOIN into Lumen's store (module isolation, §2).
/// The room comes from the entry's <see cref="AgendaEntry.RoomId"/> (card [E10.11]); its display name is a
/// same-schema, same-module <c>LEFT JOIN</c> onto <c>agenda.rooms</c>, so the Gantt can group its lanes by room.
/// Both stay <see langword="null"/> for an entry that has no room association (a non-RoomBooking never reaches
/// this query, but a RoomBooking created before a room was picked can still surface with a null lane).
/// </remarks>
public sealed record GetRoomOccupancyQuery(DateOnly Date) : IQuery<IReadOnlyList<RoomOccupancySlot>>;

/// <summary>A single occupied slot on the room-occupancy Gantt for the requested day (card [E10.11]).</summary>
public sealed record RoomOccupancySlot(
    Guid? RoomId,
    string? RoomName,
    Guid EntryId,
    string Title,
    DateTime StartUtc,
    DateTime EndUtc,
    Guid ResponsibleId,
    string ResponsibleName,
    string? Color);

internal sealed class GetRoomOccupancyQueryHandler
    : BaseDataAccess, IQueryHandler<GetRoomOccupancyQuery, IReadOnlyList<RoomOccupancySlot>>
{
    // Only RoomBooking entries can occupy a room. A recurring series that started before the day may still have
    // an occurrence on it, so it qualifies whenever its start is on or before the end of the day; the in-memory
    // expansion then keeps only the instances that actually fall on the requested date.
    private const string Sql =
        """
        SELECT
            e.id              AS id,
            e.title           AS title,
            e.start_date_utc  AS startdateutc,
            e.end_date_utc    AS enddateutc,
            e.recurrence_rule AS recurrencerule,
            e.excluded_dates  AS excludeddatesjson,
            e.responsible_id  AS responsibleid,
            e.room_id         AS roomid,
            e.color           AS color,
            r.name            AS roomname
        FROM agenda.agenda_entries e
        LEFT JOIN agenda.rooms r
               ON r.id = e.room_id
              AND r.company_id = e.company_id
        WHERE e.company_id = @CompanyId
          AND e.activity_type = 'RoomBooking'
          AND e.start_date_utc <= @DayEnd
        ORDER BY e.start_date_utc;
        """;

    private readonly ITenantContext _tenantContext;
    private readonly RecurrenceExpander _expander;
    private readonly ILumenUserGateway _userGateway;

    public GetRoomOccupancyQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext,
        RecurrenceExpander expander,
        ILumenUserGateway userGateway)
        : base(connectionFactory)
    {
        _tenantContext = tenantContext;
        _expander = expander;
        _userGateway = userGateway;
    }

    public async Task<IReadOnlyList<RoomOccupancySlot>> HandleAsync(
        GetRoomOccupancyQuery request, CancellationToken cancellationToken = default)
    {
        DateTime dayStart = request.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime dayEnd = request.Date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        using IDbConnection connection = await OpenConnectionAsync();
        IEnumerable<OccupancyRow> rows = await connection.QueryAsync<OccupancyRow>(
            new CommandDefinition(
                Sql,
                new { CompanyId = _tenantContext.CompanyId, DayEnd = dayEnd },
                cancellationToken: cancellationToken));

        List<(OccupancyRow Row, EntryOccurrence Occurrence)> onDay = ExpandOntoDay(rows, request.Date, dayStart, dayEnd);

        IReadOnlyDictionary<Guid, string> responsibleNames = await ResolveResponsibleNamesAsync(onDay, cancellationToken);

        return onDay
            .Select(pair => ToSlot(pair.Row, pair.Occurrence, responsibleNames))
            .OrderBy(slot => slot.StartUtc)
            .ThenBy(slot => slot.Title)
            .ToList();
    }

    // Expand each candidate and keep only the occurrences that fall on the requested date (EXDATE already dropped
    // by the expander). A one-off simply passes through when its own date matches.
    private List<(OccupancyRow, EntryOccurrence)> ExpandOntoDay(
        IEnumerable<OccupancyRow> rows, DateOnly date, DateTime dayStart, DateTime dayEnd)
    {
        var result = new List<(OccupancyRow, EntryOccurrence)>();

        foreach (OccupancyRow row in rows)
        {
            IReadOnlyList<DateOnly> excluded = DeserializeExcludedDates(row.ExcludedDatesJson);

            IReadOnlyList<EntryOccurrence> occurrences = _expander.Expand(
                row.StartDateUtc, row.EndDateUtc, row.RecurrenceRule, excluded, dayStart, dayEnd);

            foreach (EntryOccurrence occurrence in occurrences.Where(o => o.OccurrenceDate == date))
                result.Add((row, occurrence));
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveResponsibleNamesAsync(
        List<(OccupancyRow Row, EntryOccurrence Occurrence)> onDay, CancellationToken cancellationToken)
    {
        Guid[] responsibleIds = onDay
            .Select(pair => pair.Row.ResponsibleId)
            .Distinct()
            .ToArray();

        var names = new Dictionary<Guid, string>(responsibleIds.Length);
        foreach (Guid responsibleId in responsibleIds)
        {
            MemberEnrichmentDto? member = await _userGateway.EnrichMemberAsync(responsibleId, cancellationToken);
            if (member is not null)
                names[responsibleId] = member.Username;
        }

        return names;
    }

    private static RoomOccupancySlot ToSlot(
        OccupancyRow row, EntryOccurrence occurrence, IReadOnlyDictionary<Guid, string> responsibleNames)
        => new(
            RoomId: row.RoomId,
            RoomName: ResolveRoomName(row.RoomId, row.RoomName),
            EntryId: row.Id,
            Title: row.Title,
            StartUtc: occurrence.StartUtc,
            EndUtc: occurrence.EndUtc,
            ResponsibleId: row.ResponsibleId,
            ResponsibleName: responsibleNames.GetValueOrDefault(row.ResponsibleId, string.Empty),
            // The operator's per-entry colour override (card [E10.12]); null lets the Gantt fall back to the lane
            // (per-room) colour.
            Color: row.Color);

    /// <summary>
    /// The Gantt lane label for a slot: the joined <paramref name="joinedName"/> when the room row exists,
    /// otherwise the raw <paramref name="roomId"/> as text so an orphaned/legacy association still renders a
    /// stable lane instead of a blank one; <see langword="null"/> only when the entry has no room at all.
    /// </summary>
    internal static string? ResolveRoomName(Guid? roomId, string? joinedName)
        => joinedName ?? roomId?.ToString();

    private static IReadOnlyList<DateOnly> DeserializeExcludedDates(string? json)
        => string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<DateOnly>>(json) ?? [];

    private sealed record OccupancyRow(
        Guid Id,
        string Title,
        DateTime StartDateUtc,
        DateTime EndDateUtc,
        string? RecurrenceRule,
        string? ExcludedDatesJson,
        Guid ResponsibleId,
        Guid? RoomId,
        string? Color,
        string? RoomName);
}
