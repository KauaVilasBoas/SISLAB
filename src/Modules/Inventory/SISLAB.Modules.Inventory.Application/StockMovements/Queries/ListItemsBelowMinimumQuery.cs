using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Application.StockMovements.Queries;

/// <summary>
/// Read-side query (card [E4] #32) that lists the stock items of the <b>active company</b> whose on-hand
/// quantity has fallen below the configured minimum stock — the reposition/low-stock list that backs the
/// inventory screen and the E6 reposition-alert job. It reads the desnormalized <c>inventory.stock_view</c>
/// (item × storage location, card [E4] #33) via Dapper — never the write DbContext — projecting the flat
/// <see cref="BelowMinimumItem"/> the UI needs, ordered by <b>criticality</b> (largest deficit first).
/// </summary>
/// <remarks>
/// <para>
/// <b>Below minimum.</b> The condition mirrors the view's precomputed <c>is_below_minimum</c> column
/// (<c>quantity_amount &lt; minimum_quantity_amount</c>), so the read side and the write side agree on the
/// exact boundary — an item exactly at its minimum is <b>not</b> listed (it is not yet below). The
/// <see cref="BelowMinimumItem.Deficit"/> is <c>minimum_quantity_amount − quantity_amount</c>, always
/// positive for a listed row, expressed in the item's own unit (the minimum shares the item's canonical
/// unit, so amounts are directly comparable — the same assumption the view's comparison relies on).
/// </para>
/// <para>
/// <b>Criticality ordering.</b> Rows are ordered by descending deficit, so the most under-stocked items —
/// the ones most in need of reposition — come first, satisfying the card's "ordena por criticidade" criterion.
/// Name and id are secondary keys for a stable, deterministic page.
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from
/// the request, and every SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global
/// query filter, so the tenant guard is explicit (defense-in-depth, section 7).
/// </para>
/// </remarks>
public sealed record ListItemsBelowMinimumQuery : PagedQuery<PagedResult<BelowMinimumItem>>
{
    /// <summary>Optional storage-location filter; null lists below-minimum items of every location.</summary>
    public Guid? StorageLocationId { get; init; }
}

/// <summary>
/// Flat read row for the low-stock/reposition list (card [E4] #32). Enxuto by design: it exposes the item,
/// its current quantity, the configured minimum, the derived <see cref="Deficit"/> and its storage location —
/// never the <c>StockItem</c> aggregate or its value objects.
/// </summary>
public sealed record BelowMinimumItem(
    Guid Id,
    string Name,
    string Category,
    string? Brand,
    decimal Quantity,
    string Unit,
    decimal MinimumQuantity,
    string MinimumUnit,
    decimal Deficit,
    bool IsControlled,
    Guid StorageLocationId,
    string? StorageLocationName,
    string? StorageLocationType);

internal sealed class ListItemsBelowMinimumQueryHandler
    : BaseDataAccess, IQueryHandler<ListItemsBelowMinimumQuery, PagedResult<BelowMinimumItem>>
{
    // The below-minimum filter reuses the view's precomputed is_below_minimum flag
    // (quantity_amount < minimum_quantity_amount), so an item exactly at its minimum is not listed. The
    // deficit (minimum − quantity) is projected once and drives the criticality ordering: largest deficit
    // first, so the most under-stocked items lead the page. company_id keeps the mandatory tenant scoping.
    private const string Sql =
        """
        WITH records AS (
            SELECT
                v.id,
                v.name,
                v.category,
                v.brand,
                v.quantity_amount,
                v.quantity_unit,
                v.minimum_quantity_amount,
                v.minimum_quantity_unit,
                (v.minimum_quantity_amount - v.quantity_amount) AS deficit,
                v.is_controlled,
                v.storage_location_id,
                v.storage_location_name,
                v.storage_location_type,
                ROW_NUMBER() OVER (
                    ORDER BY (v.minimum_quantity_amount - v.quantity_amount) DESC, v.name ASC, v.id ASC
                ) AS row_number,
                (COUNT(*) OVER ())::int AS total_rows
            FROM inventory.stock_view AS v
            WHERE v.company_id = @CompanyId
              AND v.is_below_minimum
              AND (@StorageLocationId IS NULL OR v.storage_location_id = @StorageLocationId)
        )
        SELECT
            id,
            name,
            category,
            brand,
            quantity_amount         AS quantity,
            quantity_unit           AS unit,
            minimum_quantity_amount AS minimumquantity,
            minimum_quantity_unit   AS minimumunit,
            deficit,
            is_controlled           AS iscontrolled,
            storage_location_id     AS storagelocationid,
            storage_location_name   AS storagelocationname,
            storage_location_type   AS storagelocationtype,
            total_rows              AS totalrows
        FROM records
        WHERE row_number BETWEEN @FirstResult AND @LastResult
        ORDER BY row_number;
        """;

    private readonly ITenantContext _tenantContext;

    public ListItemsBelowMinimumQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<PagedResult<BelowMinimumItem>> HandleAsync(
        ListItemsBelowMinimumQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        BelowMinimumQueryParameters parameters = BuildParameters(request);

        IReadOnlyList<BelowMinimumItemRow> rows = (await connection.QueryAsync<BelowMinimumItemRow>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        int totalCount = rows.Count > 0 ? rows[0].TotalRows : 0;

        IReadOnlyList<BelowMinimumItem> items = rows
            .Select(row => row.ToBelowMinimumItem())
            .ToList();

        return new PagedResult<BelowMinimumItem>(items, totalCount, request.Page, request.PageSize);
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes from
    /// <see cref="ITenantContext"/> (never the request) — extracted so the tenant guard and pagination bounds
    /// are unit-testable without a live database.
    /// </summary>
    internal BelowMinimumQueryParameters BuildParameters(ListItemsBelowMinimumQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        StorageLocationId: request.StorageLocationId,
        FirstResult: request.FirstResult,
        LastResult: request.LastResult);

    /// <summary>
    /// Dapper materialization row: carries the per-page <c>total_rows</c> (from <c>COUNT(*) OVER()</c>)
    /// alongside the projected columns, so the total and the page come back in a single round-trip.
    /// </summary>
    private sealed record BelowMinimumItemRow(
        Guid Id,
        string Name,
        string Category,
        string? Brand,
        decimal Quantity,
        string Unit,
        decimal MinimumQuantity,
        string MinimumUnit,
        decimal Deficit,
        bool IsControlled,
        Guid StorageLocationId,
        string? StorageLocationName,
        string? StorageLocationType,
        int TotalRows)
    {
        public BelowMinimumItem ToBelowMinimumItem() => new(
            Id,
            Name,
            Category,
            Brand,
            Quantity,
            Unit,
            MinimumQuantity,
            MinimumUnit,
            Deficit,
            IsControlled,
            StorageLocationId,
            StorageLocationName,
            StorageLocationType);
    }
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="ListItemsBelowMinimumQuery"/>. The property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the
/// tenant guard and pagination can be asserted without a live database.
/// </summary>
internal sealed record BelowMinimumQueryParameters(
    Guid CompanyId,
    Guid? StorageLocationId,
    int FirstResult,
    int LastResult);
