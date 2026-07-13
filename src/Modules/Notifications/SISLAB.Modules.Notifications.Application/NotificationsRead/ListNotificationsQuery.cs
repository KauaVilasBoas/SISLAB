using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Notifications.Application.NotificationsRead;

/// <summary>
/// Read-side query (card #64a — the notification bell) that lists the notifications of the <b>active
/// company</b>, newest first, optionally filtered to only unread ones. It reads the write table
/// <c>notifications.notifications</c> directly via Dapper (no separate projection is warranted — the table is
/// already the read shape) and projects the flat <see cref="NotificationListItem"/> the bell renders, never
/// the aggregate.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from
/// the request, and every SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read side has no EF global
/// query filter, so the tenant guard is explicit (defense-in-depth, section 7).
/// </remarks>
public sealed record ListNotificationsQuery : PagedQuery<PagedResult<NotificationListItem>>
{
    /// <summary>When true, only unread notifications are returned; when false (default), all are listed.</summary>
    public bool UnreadOnly { get; init; }
}

/// <summary>
/// Flat read row for the bell (card #64a). Enxuto by design: the fields the UI shows and links from, never the
/// <c>Notification</c> aggregate or its value objects. The reference is flattened to its target type + id so
/// the bell can deep-link ("Ver item"/"Ver equipamento") without touching another module's domain.
/// </summary>
public sealed record NotificationListItem(
    Guid Id,
    string Type,
    string Severity,
    string Title,
    string Description,
    string ReferenceTargetType,
    Guid ReferenceTargetId,
    bool IsRead,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);

internal sealed class ListNotificationsQueryHandler
    : BaseDataAccess, IQueryHandler<ListNotificationsQuery, PagedResult<NotificationListItem>>
{
    // Newest-first page over the company's notifications, with COUNT(*) OVER() for the total in one round-trip.
    // @UnreadOnly narrows to is_read = false when set. company_id keeps the mandatory tenant scoping (the read
    // side has no EF global query filter). Column aliases are lowercased to match the record property names.
    private const string Sql =
        """
        WITH records AS (
            SELECT
                n.id,
                n.type,
                n.severity,
                n.title,
                n.description,
                n.reference_target_type,
                n.reference_target_id,
                n.is_read,
                n.created_at_utc,
                n.read_at_utc,
                ROW_NUMBER() OVER (ORDER BY n.created_at_utc DESC, n.id DESC) AS row_number,
                (COUNT(*)    OVER ())::int                                    AS total_rows
            FROM notifications.notifications AS n
            WHERE n.company_id = @CompanyId
              AND (@UnreadOnly = false OR n.is_read = false)
        )
        SELECT
            id,
            type,
            severity,
            title,
            description,
            reference_target_type AS referencetargettype,
            reference_target_id   AS referencetargetid,
            is_read               AS isread,
            created_at_utc        AS createdatutc,
            read_at_utc           AS readatutc,
            total_rows            AS totalrows
        FROM records
        WHERE row_number BETWEEN @FirstResult AND @LastResult
        ORDER BY row_number;
        """;

    private readonly ITenantContext _tenantContext;

    public ListNotificationsQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<PagedResult<NotificationListItem>> HandleAsync(
        ListNotificationsQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        ListNotificationsQueryParameters parameters = BuildParameters(request);

        IReadOnlyList<NotificationListRow> rows = (await connection.QueryAsync<NotificationListRow>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        int totalCount = rows.Count > 0 ? rows[0].TotalRows : 0;

        IReadOnlyList<NotificationListItem> items = rows
            .Select(row => row.ToListItem())
            .ToList();

        return new PagedResult<NotificationListItem>(items, totalCount, request.Page, request.PageSize);
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes from
    /// <see cref="ITenantContext"/> (never the request). Extracted so the tenant guard and the unread filter
    /// are unit-testable without a live database.
    /// </summary>
    internal ListNotificationsQueryParameters BuildParameters(ListNotificationsQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        UnreadOnly: request.UnreadOnly,
        FirstResult: request.FirstResult,
        LastResult: request.LastResult);

    /// <summary>
    /// Dapper materialization row: carries the per-page <c>total_rows</c> (from <c>COUNT(*) OVER()</c>)
    /// alongside the projected columns, so the total and the page come back in a single round-trip.
    /// </summary>
    private sealed record NotificationListRow(
        Guid Id,
        string Type,
        string Severity,
        string Title,
        string Description,
        string ReferenceTargetType,
        Guid ReferenceTargetId,
        bool IsRead,
        DateTime CreatedAtUtc,
        DateTime? ReadAtUtc,
        int TotalRows)
    {
        public NotificationListItem ToListItem() => new(
            Id,
            Type,
            Severity,
            Title,
            Description,
            ReferenceTargetType,
            ReferenceTargetId,
            IsRead,
            CreatedAtUtc,
            ReadAtUtc);
    }
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="ListNotificationsQuery"/>. The property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the
/// tenant guard, unread filter and pagination can be asserted without a live database.
/// </summary>
internal sealed record ListNotificationsQueryParameters(
    Guid CompanyId,
    bool UnreadOnly,
    int FirstResult,
    int LastResult);
