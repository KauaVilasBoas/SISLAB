using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Application.StockMovements.Queries;

/// <summary>
/// Read-side query (card [E7] #47) that lists the movement history (ledger) of a single stock item of the
/// <b>active company</b> — entries, consumptions, transfers and disposals — most recent first, optionally
/// narrowed by movement type and/or a business-date window, paginated. It reads the projected
/// <c>inventory.stock_movements</c> table (one row per movement, fed asynchronously from the Outbox — card
/// [E7] #47) via Dapper — never the write DbContext — and projects the flat <see cref="StockMovementListItem"/>
/// the movements table needs.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from
/// the request, and every SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global
/// query filter, so the tenant guard is explicit (defense-in-depth, section 7). The item id is a required
/// filter: the ledger is always scoped to one item.
/// </para>
/// <para>
/// <b>Ordering.</b> Rows are ordered by <c>occurred_on</c> descending (the operator's business date), with
/// <c>created_at_utc</c> descending as a stable tie-breaker so movements sharing a date keep the order in
/// which they were projected, giving a deterministic page.
/// </para>
/// <para>
/// <b>performed_by (responsável).</b> Comes straight from the read model, where it is null for now: the
/// Inventory module has no user identity (decision on card [E3] #24); the audit trail (E9) owns it and may
/// backfill later. The UI renders a placeholder when it is null.
/// </para>
/// </remarks>
public sealed record ListStockMovementsQuery : PagedQuery<PagedResult<StockMovementListItem>>
{
    /// <summary>The stock item whose movements are listed. Required — the ledger is per item.</summary>
    public Guid StockItemId { get; init; }

    /// <summary>
    /// Optional movement-type filter (matched against the persisted discriminator: <c>Received</c>,
    /// <c>Consumed</c>, <c>Transferred</c>, <c>Disposed</c>); null lists every type.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>Optional inclusive lower bound on the movement's business date; null means unbounded.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Optional inclusive upper bound on the movement's business date; null means unbounded.</summary>
    public DateOnly? To { get; init; }
}

/// <summary>
/// Flat read row for the movements table (card [E7] #47). Enxuto by design: it exposes primitives the UI
/// renders directly and never leaks the <c>StockItem</c> aggregate or its value objects. <see cref="Type"/>
/// is the movement discriminator as a string; <see cref="PerformedBy"/> is null while the module has no
/// operator identity.
/// </summary>
public sealed record StockMovementListItem(
    Guid Id,
    Guid StockItemId,
    string Type,
    decimal Quantity,
    string Unit,
    DateOnly OccurredAt,
    string? Notes,
    Guid? PerformedBy);

internal sealed class ListStockMovementsQueryHandler
    : BaseDataAccess, IQueryHandler<ListStockMovementsQuery, PagedResult<StockMovementListItem>>
{
    // The ledger is scoped to one item of the active company. The optional type filter matches the persisted
    // movement_type discriminator; the from/to filters bound occurred_on (the operator's business date).
    // Ordering is occurred_on DESC with created_at_utc DESC as a stable tie-breaker, so same-date movements
    // keep projection order and the page is deterministic. company_id keeps the mandatory tenant scoping.
    // The read model has no notes column yet, so notes is projected as NULL to keep the row shape stable.
    private const string Sql =
        """
        WITH records AS (
            SELECT
                m.id,
                m.stock_item_id,
                m.movement_type,
                m.quantity_amount,
                m.quantity_unit,
                m.occurred_on,
                m.performed_by,
                ROW_NUMBER() OVER (ORDER BY m.occurred_on DESC, m.created_at_utc DESC) AS row_number,
                (COUNT(*) OVER ())::int AS total_rows
            FROM inventory.stock_movements AS m
            WHERE m.company_id = @CompanyId
              AND m.stock_item_id = @StockItemId
              AND (@Type IS NULL OR m.movement_type = @Type)
              AND (@From IS NULL OR m.occurred_on >= @From)
              AND (@To IS NULL OR m.occurred_on <= @To)
        )
        SELECT
            id,
            stock_item_id   AS stockitemid,
            movement_type   AS type,
            quantity_amount AS quantity,
            quantity_unit   AS unit,
            occurred_on     AS occurredat,
            NULL            AS notes,
            performed_by    AS performedby,
            total_rows      AS totalrows
        FROM records
        WHERE row_number BETWEEN @FirstResult AND @LastResult
        ORDER BY row_number;
        """;

    private readonly ITenantContext _tenantContext;

    public ListStockMovementsQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<PagedResult<StockMovementListItem>> HandleAsync(
        ListStockMovementsQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        StockMovementsQueryParameters parameters = BuildParameters(request);

        IReadOnlyList<StockMovementItemRow> rows = (await connection.QueryAsync<StockMovementItemRow>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        int totalCount = rows.Count > 0 ? rows[0].TotalRows : 0;

        IReadOnlyList<StockMovementListItem> items = rows
            .Select(row => row.ToListItem())
            .ToList();

        return new PagedResult<StockMovementListItem>(items, totalCount, request.Page, request.PageSize);
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes from
    /// <see cref="ITenantContext"/> (never the request), and a blank type filter collapses to null (an empty
    /// selection means "no filter"). Extracted so the tenant guard, filter normalization and pagination bounds
    /// are unit-testable without a live database.
    /// </summary>
    internal StockMovementsQueryParameters BuildParameters(ListStockMovementsQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        StockItemId: request.StockItemId,
        Type: NormalizeFilter(request.Type),
        From: request.From,
        To: request.To,
        FirstResult: request.FirstResult,
        LastResult: request.LastResult);

    /// <summary>Trims a filter and collapses a blank value to null, so an empty selection means "no filter".</summary>
    private static string? NormalizeFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Dapper materialization row: carries the per-page <c>total_rows</c> (from <c>COUNT(*) OVER()</c>)
    /// alongside the projected columns, so the total and the page come back in a single round-trip.
    /// </summary>
    private sealed record StockMovementItemRow(
        Guid Id,
        Guid StockItemId,
        string Type,
        decimal Quantity,
        string Unit,
        DateOnly OccurredAt,
        string? Notes,
        Guid? PerformedBy,
        int TotalRows)
    {
        public StockMovementListItem ToListItem() => new(
            Id,
            StockItemId,
            Type,
            Quantity,
            Unit,
            OccurredAt,
            Notes,
            PerformedBy);
    }
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="ListStockMovementsQuery"/>. The property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the
/// tenant guard, filter normalization and pagination can be asserted without a live database.
/// </summary>
internal sealed record StockMovementsQueryParameters(
    Guid CompanyId,
    Guid StockItemId,
    string? Type,
    DateOnly? From,
    DateOnly? To,
    int FirstResult,
    int LastResult);
