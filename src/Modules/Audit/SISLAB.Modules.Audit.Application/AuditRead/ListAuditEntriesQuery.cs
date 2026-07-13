using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Audit.Application.AuditRead;

/// <summary>
/// Read-side query (card [E9] #57) that lists the audit trail of the <b>active company</b>, newest first,
/// with optional filters by entity type, action and an inclusive occurred-at date window. Reads
/// <c>audit.audit_entries</c> directly via Dapper (the table is already the read shape) and projects the
/// flat <see cref="AuditEntryListItem"/> the compliance screen renders.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from
/// the request, and every SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read side has no EF global
/// query filter, so the tenant guard is explicit (defense-in-depth, section 7).
/// </remarks>
public sealed record ListAuditEntriesQuery : PagedQuery<PagedResult<AuditEntryListItem>>
{
    /// <summary>Optional filter: only entries affecting this entity type (e.g. <c>StockItem</c>, <c>Equipment</c>).</summary>
    public string? EntityType { get; init; }

    /// <summary>Optional filter: only entries of this business action (e.g. <c>consumption</c>, <c>maintenance</c>).</summary>
    public string? Action { get; init; }

    /// <summary>Optional inclusive lower bound on the occurred-at date (UTC).</summary>
    public DateOnly? From { get; init; }

    /// <summary>Optional inclusive upper bound on the occurred-at date (UTC).</summary>
    public DateOnly? To { get; init; }
}

/// <summary>Flat read row for the audit trail screen (card [E9] #57).</summary>
public sealed record AuditEntryListItem(
    Guid Id,
    string UserId,
    string Action,
    string EntityType,
    Guid EntityId,
    string Payload,
    DateTime OccurredAtUtc);

internal sealed class ListAuditEntriesQueryHandler
    : BaseDataAccess, IQueryHandler<ListAuditEntriesQuery, PagedResult<AuditEntryListItem>>
{
    // Newest-first page over the company's audit trail, with COUNT(*) OVER() for the total in one round-trip.
    // Every filter is optional and applied only when supplied (@Param IS NULL OR ...). company_id keeps the
    // mandatory tenant scoping (the read side has no EF global query filter).
    private const string Sql =
        """
        WITH records AS (
            SELECT
                a.id,
                a.user_id,
                a.action,
                a.entity_type,
                a.entity_id,
                a.payload,
                a.occurred_at_utc,
                ROW_NUMBER() OVER (ORDER BY a.occurred_at_utc DESC, a.id DESC) AS row_number,
                (COUNT(*)    OVER ())::int                                     AS total_rows
            FROM audit.audit_entries AS a
            WHERE a.company_id = @CompanyId
              AND (@EntityType IS NULL OR a.entity_type = @EntityType)
              AND (@Action     IS NULL OR a.action = @Action)
              AND (@From       IS NULL OR a.occurred_at_utc >= @From)
              AND (@To         IS NULL OR a.occurred_at_utc < @ToExclusive)
        )
        SELECT
            id,
            user_id         AS userid,
            action,
            entity_type     AS entitytype,
            entity_id       AS entityid,
            payload,
            occurred_at_utc AS occurredatutc,
            total_rows      AS totalrows
        FROM records
        WHERE row_number BETWEEN @FirstResult AND @LastResult
        ORDER BY row_number;
        """;

    private readonly ITenantContext _tenantContext;

    public ListAuditEntriesQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<PagedResult<AuditEntryListItem>> HandleAsync(
        ListAuditEntriesQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        AuditEntriesQueryParameters parameters = BuildParameters(request);

        IReadOnlyList<AuditEntryRow> rows = (await connection.QueryAsync<AuditEntryRow>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        int totalCount = rows.Count > 0 ? rows[0].TotalRows : 0;

        IReadOnlyList<AuditEntryListItem> items = rows
            .Select(row => row.ToListItem())
            .ToList();

        return new PagedResult<AuditEntryListItem>(items, totalCount, request.Page, request.PageSize);
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes
    /// from <see cref="ITenantContext"/> (never the request); the pagination bounds come from the query.
    /// Extracted so the tenant guard, filters and window can be asserted without a live database.
    /// </summary>
    internal AuditEntriesQueryParameters BuildParameters(ListAuditEntriesQuery request) =>
        AuditEntriesQueryParameters.ForPage(_tenantContext.CompanyId, request);

    /// <summary>Dapper materialization row carrying the per-page <c>total_rows</c> alongside the projection.</summary>
    private sealed record AuditEntryRow(
        Guid Id,
        string UserId,
        string Action,
        string EntityType,
        Guid EntityId,
        string Payload,
        DateTime OccurredAtUtc,
        int TotalRows)
    {
        public AuditEntryListItem ToListItem() =>
            new(Id, UserId, Action, EntityType, EntityId, Payload, OccurredAtUtc);
    }
}
