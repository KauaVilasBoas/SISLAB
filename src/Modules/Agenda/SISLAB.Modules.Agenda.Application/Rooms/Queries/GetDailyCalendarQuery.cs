using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Agenda.Application.Rooms.Queries;

/// <summary>
/// Returns all active bookings for the given date across all rooms (card [E10] #69 — daily agenda view).
/// Tenant-scoped; ordered by room then start time.
/// </summary>
public sealed record GetDailyCalendarQuery(DateOnly Date) : IQuery<IReadOnlyList<BookingListItem>>;

public sealed record BookingListItem(
    Guid BookingId,
    Guid RoomId,
    string RoomName,
    string BookedByName,
    string Activity,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? Notes,
    bool HasConflictWarning);

internal sealed class GetDailyCalendarQueryHandler
    : BaseDataAccess, IQueryHandler<GetDailyCalendarQuery, IReadOnlyList<BookingListItem>>
{
    private const string Sql =
        """
        SELECT
            b.id              AS bookingid,
            b.room_id         AS roomid,
            r.name            AS roomname,
            b.booked_by_name  AS bookedbyname,
            b.activity        AS activity,
            b.date            AS date,
            b.start_time      AS starttime,
            b.end_time        AS endtime,
            b.notes           AS notes,
            b.has_conflict_warning AS hasconflictwarning
        FROM agenda.bookings b
        INNER JOIN agenda.rooms r ON r.id = b.room_id
        WHERE b.company_id = @CompanyId
          AND b.date = @Date
          AND b.status = 'Active'
        ORDER BY r.name, b.start_time;
        """;

    private readonly ITenantContext _tenantContext;

    public GetDailyCalendarQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory) => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<BookingListItem>> HandleAsync(
        GetDailyCalendarQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();
        return (await connection.QueryAsync<BookingListItem>(
            new CommandDefinition(Sql,
                new { CompanyId = _tenantContext.CompanyId, request.Date },
                cancellationToken: cancellationToken))).AsList();
    }
}
