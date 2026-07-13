using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Application.StockMovements.Queries;

/// <summary>
/// Read-side query (card [E4] #29) that lists the stock items of the <b>active company</b> for the
/// inventory master-detail screen (#46), filtered by storage location and/or category, with optional
/// free-text search and pagination. It reads the desnormalized <c>inventory.stock_view</c> (item ×
/// storage location, card [E4] #33) via Dapper — never the write DbContext — and projects the flat
/// <see cref="StockItemListItem"/> the UI table and detail panel need.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never
/// from the request, and every SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no
/// EF global query filter, so the tenant guard is explicit (defense-in-depth, section 7).
/// </para>
/// <para>
/// <b>Derived expiry status.</b> Validity is stored with month granularity (<c>expiry_year</c> /
/// <c>expiry_month</c>); the item expires on the last day of that month. The status is computed in SQL
/// against the handler-supplied <c>@Today</c> and a 30-day warning window — a faithful mirror of
/// <see cref="ExpiryStatusRule"/> — and is <see cref="ExpiryStatusView.NotApplicable"/> when the item
/// has no validity. It is never persisted (decision recorded on the StorageLocation aggregate / card #29).
/// </para>
/// </remarks>
public sealed record ListStockItemsQuery : PagedQuery<PagedResult<StockItemListItem>>
{
    /// <summary>Optional storage-location filter; null lists items of every location.</summary>
    public Guid? StorageLocationId { get; init; }

    /// <summary>Optional category filter (the <c>StockItemCategory</c> enum name); null lists every category.</summary>
    public string? Category { get; init; }

    /// <summary>Optional free-text search matched (ILIKE) against name, lot code and brand.</summary>
    public string? Search { get; init; }
}

/// <summary>Derived expiry classification of a stock item, as exposed to the read side.</summary>
public enum ExpiryStatusView
{
    /// <summary>The item has no recorded validity.</summary>
    NotApplicable,

    /// <summary>Valid and outside the warning window.</summary>
    Ok,

    /// <summary>Valid but within the warning window (about to expire).</summary>
    ExpiringSoon,

    /// <summary>Past its last valid day.</summary>
    Expired
}

/// <summary>
/// Flat read row for the inventory table and detail panel (card [E4] #29). Enxuto by design: it exposes
/// primitives the UI renders directly and never leaks the <c>StockItem</c> aggregate or its value objects.
/// </summary>
public sealed record StockItemListItem(
    Guid Id,
    string Name,
    string Category,
    string? Brand,
    string? LotCode,
    decimal Quantity,
    string Unit,
    decimal MinimumQuantity,
    string MinimumUnit,
    bool IsBelowMinimum,
    int? ExpiryYear,
    int? ExpiryMonth,
    ExpiryStatusView ExpiryStatus,
    string ContainerState,
    bool IsControlled,
    Guid StorageLocationId,
    string? StorageLocationName,
    string? StorageLocationType,
    string? Application);

internal sealed class ListStockItemsQueryHandler
    : BaseDataAccess, IQueryHandler<ListStockItemsQuery, PagedResult<StockItemListItem>>
{
    // The SQL CASE deriving the expiry status is a faithful mirror of ExpiryStatusRule.Classify: the
    // item's last valid day is the last day of (expiry_year, expiry_month); it is Expired past that day,
    // ExpiringSoon when that day is within the warning window from @Today, otherwise Ok — and
    // NotApplicable when there is no validity. Each branch returns the matching ExpiryStatusView ordinal
    // so Dapper maps the column straight to the enum. All @Not*/@Ok/@Expiring*/@Expired params carry
    // those ordinals; @Today and @WarningWindowDays come from the handler (IClock), never the DB clock.
    private const string Sql =
        """
        WITH records AS (
            SELECT
                v.id,
                v.name,
                v.category,
                v.brand,
                v.lot_code,
                v.quantity_amount,
                v.quantity_unit,
                v.minimum_quantity_amount,
                v.minimum_quantity_unit,
                v.is_below_minimum,
                v.expiry_year,
                v.expiry_month,
                CASE
                    WHEN v.expiry_year IS NULL OR v.expiry_month IS NULL THEN @NotApplicable
                    WHEN @Today > (make_date(v.expiry_year, v.expiry_month, 1)
                                   + INTERVAL '1 month' - INTERVAL '1 day')::date THEN @Expired
                    WHEN (make_date(v.expiry_year, v.expiry_month, 1)
                          + INTERVAL '1 month' - INTERVAL '1 day')::date
                         <= (@Today + (@WarningWindowDays || ' days')::interval)::date THEN @ExpiringSoon
                    ELSE @Ok
                END AS expiry_status,
                v.container_state,
                v.is_controlled,
                v.storage_location_id,
                v.storage_location_name,
                v.storage_location_type,
                v.application,
                ROW_NUMBER() OVER (ORDER BY v.name ASC, v.id ASC) AS row_number,
                (COUNT(*)    OVER ())::int                        AS total_rows
            FROM inventory.stock_view AS v
            WHERE v.company_id = @CompanyId
              AND (@StorageLocationId IS NULL OR v.storage_location_id = @StorageLocationId)
              AND (@Category IS NULL OR v.category = @Category)
              AND (
                    @Search IS NULL
                    OR v.name ILIKE '%' || @Search || '%'
                    OR v.lot_code ILIKE '%' || @Search || '%'
                    OR v.brand ILIKE '%' || @Search || '%'
                  )
        )
        SELECT
            id,
            name,
            category,
            brand,
            lot_code                AS lotcode,
            quantity_amount         AS quantity,
            quantity_unit           AS unit,
            minimum_quantity_amount AS minimumquantity,
            minimum_quantity_unit   AS minimumunit,
            is_below_minimum        AS isbelowminimum,
            expiry_year             AS expiryyear,
            expiry_month            AS expirymonth,
            expiry_status           AS expirystatus,
            container_state         AS containerstate,
            is_controlled           AS iscontrolled,
            storage_location_id     AS storagelocationid,
            storage_location_name   AS storagelocationname,
            storage_location_type   AS storagelocationtype,
            application,
            total_rows              AS totalrows
        FROM records
        WHERE row_number BETWEEN @FirstResult AND @LastResult
        ORDER BY row_number;
        """;

    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public ListStockItemsQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext,
        IClock clock)
        : base(connectionFactory)
    {
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<PagedResult<StockItemListItem>> HandleAsync(
        ListStockItemsQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        StockItemsQueryParameters parameters = BuildParameters(request);

        IReadOnlyList<StockItemRow> rows = (await connection.QueryAsync<StockItemRow>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        int totalCount = rows.Count > 0 ? rows[0].TotalRows : 0;

        IReadOnlyList<StockItemListItem> items = rows
            .Select(row => row.ToListItem())
            .ToList();

        return new PagedResult<StockItemListItem>(items, totalCount, request.Page, request.PageSize);
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes
    /// from <see cref="ITenantContext"/> (never the request), blank filters collapse to null (an empty box
    /// means "no filter"), and <c>@Today</c> is derived from the injected <see cref="IClock"/> — extracted
    /// so the tenant guard and filter normalization are unit-testable without a live database.
    /// </summary>
    internal StockItemsQueryParameters BuildParameters(ListStockItemsQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        StorageLocationId: request.StorageLocationId,
        Category: NormalizeFilter(request.Category),
        Search: NormalizeFilter(request.Search),
        Today: DateOnly.FromDateTime(_clock.UtcNow),
        WarningWindowDays: ExpiryStatusRule.DefaultWarningWindowDays,
        NotApplicable: (int)ExpiryStatusView.NotApplicable,
        Ok: (int)ExpiryStatusView.Ok,
        ExpiringSoon: (int)ExpiryStatusView.ExpiringSoon,
        Expired: (int)ExpiryStatusView.Expired,
        FirstResult: request.FirstResult,
        LastResult: request.LastResult);

    /// <summary>Trims a filter and collapses a blank value to null, so an empty box means "no filter".</summary>
    private static string? NormalizeFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Dapper materialization row: carries the per-page <c>total_rows</c> (from <c>COUNT(*) OVER()</c>)
    /// alongside the projected columns, so the total and the page come back in a single round-trip.
    /// </summary>
    private sealed record StockItemRow(
        Guid Id,
        string Name,
        string Category,
        string? Brand,
        string? LotCode,
        decimal Quantity,
        string Unit,
        decimal MinimumQuantity,
        string MinimumUnit,
        bool IsBelowMinimum,
        int? ExpiryYear,
        int? ExpiryMonth,
        ExpiryStatusView ExpiryStatus,
        string ContainerState,
        bool IsControlled,
        Guid StorageLocationId,
        string? StorageLocationName,
        string? StorageLocationType,
        string? Application,
        int TotalRows)
    {
        public StockItemListItem ToListItem() => new(
            Id,
            Name,
            Category,
            Brand,
            LotCode,
            Quantity,
            Unit,
            MinimumQuantity,
            MinimumUnit,
            IsBelowMinimum,
            ExpiryYear,
            ExpiryMonth,
            ExpiryStatus,
            ContainerState,
            IsControlled,
            StorageLocationId,
            StorageLocationName,
            StorageLocationType,
            Application);
    }
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="ListStockItemsQuery"/>. The property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the
/// tenant guard and filter normalization can be asserted without a live database.
/// </summary>
internal sealed record StockItemsQueryParameters(
    Guid CompanyId,
    Guid? StorageLocationId,
    string? Category,
    string? Search,
    DateOnly Today,
    int WarningWindowDays,
    int NotApplicable,
    int Ok,
    int ExpiringSoon,
    int Expired,
    int FirstResult,
    int LastResult);
