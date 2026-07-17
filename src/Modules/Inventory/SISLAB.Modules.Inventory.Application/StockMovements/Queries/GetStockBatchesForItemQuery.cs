using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Application.StockMovements.Queries;

/// <summary>
/// Read-side query (card [E7] #111) that lists the <b>available batches</b> (remaining balance &gt; 0) of a
/// single stock item of the <b>active company</b>, ordered <b>FEFO</b> (first-expired-first-out; batches
/// without an expiry sort last), so the consumption lot picker can default to the batch the aggregate would
/// draw next and show each lot's validity, remaining balance and unit cost. It reads the write-side
/// <c>inventory.stock_batches</c> table (joined to <c>stock_items</c> for the tenant scope) via Dapper —
/// never the write DbContext.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the batch table directly (not a projected read model).</b> Batches are current-state write data
/// (remaining balance, lot, expiry, cost) that must be exact for the picker — an eventual projection could
/// show a stale balance and let the operator pick a depleted lot. Reading the write table (tenant-scoped by
/// the join to <c>stock_items</c>) is always consistent and matches how <c>stock_view</c> already reads the
/// same tables for the current-state listing.
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The company comes from <see cref="ITenantContext"/> (never the request); the SELECT
/// keeps <c>WHERE si.company_id = @CompanyId</c> — the read side has no EF global query filter, so the tenant
/// guard is explicit (defense-in-depth, section 7). An item of another tenant simply yields no rows.
/// </para>
/// </remarks>
public sealed record GetStockBatchesForItemQuery(Guid StockItemId) : IQuery<IReadOnlyList<StockBatchItem>>;

/// <summary>
/// One available batch of an item for the consumption lot picker (card [E7] #111). Enxuto by design: flat
/// primitives the UI renders directly — the lot code, month-granularity validity, remaining balance with its
/// unit, the unit cost (null for donations) and when it was received.
/// </summary>
public sealed record StockBatchItem(
    Guid BatchId,
    string? LotCode,
    int? ExpiryYear,
    int? ExpiryMonth,
    decimal RemainingQuantity,
    string Unit,
    decimal? UnitCostBrl,
    DateTime ReceivedAtUtc);

internal sealed class GetStockBatchesForItemQueryHandler
    : BaseDataAccess, IQueryHandler<GetStockBatchesForItemQuery, IReadOnlyList<StockBatchItem>>
{
    // Available batches (remaining > 0) of one item of the active company, FEFO: batches with no expiry sort
    // last (expiry_year IS NULL first-key), then earliest (year, month), then earliest receipt as a stable
    // tie-breaker — the exact order the aggregate draws down, so the picker's first row is the FEFO default.
    // The join to stock_items carries the mandatory company_id tenant scope (the batch table has none of its
    // own) and the item unit (batches store their amount; the unit is the item's).
    private const string Sql =
        """
        SELECT
            sb.id                        AS batchid,
            sb.lot_code                  AS lotcode,
            sb.expiry_year               AS expiryyear,
            sb.expiry_month              AS expirymonth,
            sb.remaining_quantity_amount AS remainingquantity,
            si.unit                      AS unit,
            sb.unit_cost_brl             AS unitcostbrl,
            sb.received_at_utc           AS receivedatutc
        FROM inventory.stock_batches AS sb
        JOIN inventory.stock_items AS si
            ON si.id = sb.stock_item_id
        WHERE si.company_id = @CompanyId
          AND sb.stock_item_id = @StockItemId
          AND sb.remaining_quantity_amount > 0
        ORDER BY
            (sb.expiry_year IS NULL) ASC,
            sb.expiry_year ASC,
            sb.expiry_month ASC,
            sb.received_at_utc ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public GetStockBatchesForItemQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<StockBatchItem>> HandleAsync(
        GetStockBatchesForItemQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        StockBatchesQueryParameters parameters = BuildParameters(request);

        IReadOnlyList<StockBatchItem> batches = (await connection.QueryAsync<StockBatchItem>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        return batches;
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes from
    /// <see cref="ITenantContext"/> (never the request) — extracted so the tenant guard is unit-testable
    /// without a live database.
    /// </summary>
    internal StockBatchesQueryParameters BuildParameters(GetStockBatchesForItemQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        StockItemId: request.StockItemId);
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="GetStockBatchesForItemQuery"/>. The property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the
/// tenant guard can be asserted without a live database.
/// </summary>
internal sealed record StockBatchesQueryParameters(
    Guid CompanyId,
    Guid StockItemId);
