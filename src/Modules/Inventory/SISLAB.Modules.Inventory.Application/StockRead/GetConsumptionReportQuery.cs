using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Application.StockRead;

/// <summary>
/// Read-side query (card [E4] #31) that builds the <b>consumption report</b> of the <b>active company</b>:
/// how much of each stock item was consumed within a date range, optionally narrowed to one experiment
/// and/or one category. It reads the projected ledger <c>inventory.stock_movements</c> (card [E4] #33) via
/// Dapper — never the write DbContext — keeping only the consumption movements
/// (<c>movement_type = 'Consumed'</c>), and joins <c>inventory.stock_items</c> to bring the item's
/// <c>name</c> and <c>category</c> (the ledger stores neither).
/// </summary>
/// <remarks>
/// <para>
/// <b>Aggregation grain — respect the unit.</b> Movements of the same item can be recorded in different
/// units (e.g. one entry in <c>mL</c>, another in <c>L</c>); the read side does <b>not</b> convert between
/// units, so summing across units would be meaningless. The grain is therefore
/// <c>(stock_item_id, quantity_unit)</c>: each report row carries its own <see cref="ConsumptionReportItem.Unit"/>
/// and the <see cref="ConsumptionReportItem.TotalConsumed"/> summed within that unit, plus the
/// <see cref="ConsumptionReportItem.MovementCount"/> of movements that fed it. An item consumed in two
/// units appears as two rows.
/// </para>
/// <para>
/// <b>Grand totals.</b> Alongside the (paginated) rows the query returns <see cref="ConsumptionReport.Totals"/>:
/// one total per <c>unit</c> over <b>every</b> movement in the filtered period — not just the current page —
/// so the UI footer shows the true period total without a second request. Consumptions with no experiment
/// count toward the grand total whenever the experiment filter is not applied (an unfiltered report is the
/// company-wide consumption); when an experiment is given, only that experiment's consumption is reported.
/// </para>
/// <para>
/// <b>Ordering.</b> Rows are ordered by descending consumed amount within each unit, then by name — the
/// heaviest-consumed items lead, which is what a "what did we burn through" report wants. Name and id are
/// secondary keys for a stable, deterministic page.
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from
/// the request, and every SELECT (both the ledger and the joined <c>stock_items</c>) keeps
/// <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global query filter, so the tenant guard
/// is explicit (defense-in-depth, section 7).
/// </para>
/// </remarks>
public sealed record GetConsumptionReportQuery : PagedQuery<ConsumptionReport>
{
    /// <summary>Inclusive first day of the reporting window (matched against <c>occurred_on</c>).</summary>
    public DateOnly From { get; init; }

    /// <summary>Inclusive last day of the reporting window (matched against <c>occurred_on</c>).</summary>
    public DateOnly To { get; init; }

    /// <summary>Optional experiment filter; null reports company-wide consumption (with and without experiment).</summary>
    public Guid? ExperimentId { get; init; }

    /// <summary>Optional category filter (the <c>StockItemCategory</c> enum name); null reports every category.</summary>
    public string? Category { get; init; }
}

/// <summary>
/// The consumption report for the active company (card [E4] #31): the paginated per-item rows plus the
/// per-unit grand totals over the whole filtered period.
/// </summary>
public sealed record ConsumptionReport(PagedResult<ConsumptionReportItem> Items, IReadOnlyList<ConsumptionTotal> Totals);

/// <summary>
/// One report row: the consumption of a single item, in a single unit, over the reporting window. Enxuto by
/// design — it exposes the primitives the report table renders and never leaks the write aggregate.
/// </summary>
public sealed record ConsumptionReportItem(
    Guid StockItemId,
    string Name,
    string Category,
    decimal TotalConsumed,
    string Unit,
    int MovementCount);

/// <summary>
/// A per-unit grand total over the whole filtered period (not just the current page): the total consumed in
/// that unit and how many movements fed it. Kept per-unit because the read side never converts between units.
/// </summary>
public sealed record ConsumptionTotal(string Unit, decimal TotalConsumed, int MovementCount);

internal sealed class GetConsumptionReportQueryHandler
    : BaseDataAccess, IQueryHandler<GetConsumptionReportQuery, ConsumptionReport>
{
    /// <summary>The persisted <c>movement_type</c> discriminator for a consumption (see the projection).</summary>
    private const string ConsumedMovementType = "Consumed";

    // The filtered consumption ledger, shared verbatim by both statements below. A PostgreSQL WITH clause
    // is scoped to the single statement it prefixes, so the CTE is defined once here and prepended to each
    // statement (the parameters are identical, so the two derivations read the exact same filtered set).
    private const string ConsumptionCte =
        """
        WITH consumption AS (
            SELECT
                m.stock_item_id,
                si.name              AS name,
                si.category          AS category,
                m.quantity_unit,
                m.quantity_amount
            FROM inventory.stock_movements AS m
            JOIN inventory.stock_items AS si
                ON si.id = m.stock_item_id
               AND si.company_id = m.company_id
            WHERE m.company_id = @CompanyId
              AND m.movement_type = @ConsumedMovementType
              AND m.occurred_on IS NOT NULL
              AND m.occurred_on BETWEEN @From AND @To
              AND (@ExperimentId IS NULL OR m.experiment_id = @ExperimentId)
              AND (@Category IS NULL OR si.category = @Category)
        )
        """;

    // Two result sets in one round-trip (Dapper QueryMultiple):
    //   1) the paginated per-(item, unit) rows — aggregated in the "aggregated" CTE, then windowed with
    //      ROW_NUMBER()/COUNT(*) OVER() and sliced by @FirstResult/@LastResult;
    //   2) the per-unit grand totals over EVERY movement in the filtered period (not just the page).
    // Both read the same filtered ledger (the shared ConsumptionCte): consumption movements only, the tenant
    // company, the [@From, @To] window over occurred_on, and the optional experiment/category filters. The
    // JOIN to stock_items (also tenant-scoped) brings name/category the ledger does not store. Grain is
    // (item, unit) — the read side never converts between units, so amounts are summed within a unit only.
    // Counts are cast to int: PostgreSQL COUNT is bigint, but the read models expose them as int.
    private const string Sql =
        ConsumptionCte +
        """
        ,
        aggregated AS (
            SELECT
                stock_item_id,
                name,
                category,
                quantity_unit,
                SUM(quantity_amount) AS total_consumed,
                COUNT(*)             AS movement_count
            FROM consumption
            GROUP BY stock_item_id, name, category, quantity_unit
        ),
        records AS (
            SELECT
                stock_item_id,
                name,
                category,
                total_consumed,
                quantity_unit,
                movement_count,
                ROW_NUMBER() OVER (
                    ORDER BY quantity_unit ASC, total_consumed DESC, name ASC, stock_item_id ASC
                ) AS row_number,
                (COUNT(*) OVER ())::int AS total_rows
            FROM aggregated
        )
        SELECT
            stock_item_id       AS stockitemid,
            name,
            category,
            total_consumed      AS totalconsumed,
            quantity_unit       AS unit,
            movement_count::int AS movementcount,
            total_rows          AS totalrows
        FROM records
        WHERE row_number BETWEEN @FirstResult AND @LastResult
        ORDER BY row_number;

        """ +
        ConsumptionCte +
        """

        SELECT
            quantity_unit        AS unit,
            SUM(quantity_amount) AS totalconsumed,
            COUNT(*)::int        AS movementcount
        FROM consumption
        GROUP BY quantity_unit
        ORDER BY quantity_unit ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public GetConsumptionReportQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<ConsumptionReport> HandleAsync(
        GetConsumptionReportQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        ConsumptionReportQueryParameters parameters = BuildParameters(request);

        using SqlMapper.GridReader reader = await connection.QueryMultipleAsync(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken));

        IReadOnlyList<ConsumptionReportRow> rows = (await reader.ReadAsync<ConsumptionReportRow>()).AsList();
        IReadOnlyList<ConsumptionTotal> totals = (await reader.ReadAsync<ConsumptionTotal>()).AsList();

        int totalCount = rows.Count > 0 ? rows[0].TotalRows : 0;

        IReadOnlyList<ConsumptionReportItem> items = rows
            .Select(row => row.ToReportItem())
            .ToList();

        var page = new PagedResult<ConsumptionReportItem>(items, totalCount, request.Page, request.PageSize);

        return new ConsumptionReport(page, totals);
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes from
    /// <see cref="ITenantContext"/> (never the request), blank category collapses to null, and the consumption
    /// discriminator is pinned — extracted so the tenant guard, filter normalization and pagination bounds are
    /// unit-testable without a live database.
    /// </summary>
    internal ConsumptionReportQueryParameters BuildParameters(GetConsumptionReportQuery request)
    {
        ConsumptionWindow.EnsureValid(request.From, request.To);

        return new ConsumptionReportQueryParameters(
            CompanyId: _tenantContext.CompanyId,
            ConsumedMovementType: ConsumedMovementType,
            From: request.From,
            To: request.To,
            ExperimentId: request.ExperimentId,
            Category: NormalizeFilter(request.Category),
            FirstResult: request.FirstResult,
            LastResult: request.LastResult);
    }

    /// <summary>Trims a filter and collapses a blank value to null, so an empty box means "no filter".</summary>
    private static string? NormalizeFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Dapper materialization row: carries the per-page <c>total_rows</c> (from <c>COUNT(*) OVER()</c>)
    /// alongside the projected columns, so the total and the page come back in the same result set.
    /// </summary>
    private sealed record ConsumptionReportRow(
        Guid StockItemId,
        string Name,
        string Category,
        decimal TotalConsumed,
        string Unit,
        int MovementCount,
        int TotalRows)
    {
        public ConsumptionReportItem ToReportItem() => new(
            StockItemId,
            Name,
            Category,
            TotalConsumed,
            Unit,
            MovementCount);
    }
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="GetConsumptionReportQuery"/>. The property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the
/// tenant guard, filter normalization and pagination can be asserted without a live database.
/// </summary>
internal sealed record ConsumptionReportQueryParameters(
    Guid CompanyId,
    string ConsumedMovementType,
    DateOnly From,
    DateOnly To,
    Guid? ExperimentId,
    string? Category,
    int FirstResult,
    int LastResult);
