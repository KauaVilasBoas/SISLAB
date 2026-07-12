using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Application.StockRead;

/// <summary>
/// Read-side query (card [E4] #30) that lists the stock items of the <b>active company</b> whose validity
/// is at risk — expiring within an N-day window and, optionally, already expired — for the expiry screen,
/// the amber/red validity column and (per company, with 30/15/7-day windows) the E6 validity job (#41). It
/// reads the desnormalized <c>inventory.stock_view</c> (item × storage location, card [E4] #33) via Dapper,
/// projecting the flat <see cref="ExpiringItem"/> the UI needs, ordered by validity ascending (soonest and
/// already-expired first).
/// </summary>
/// <remarks>
/// <para>
/// <b>Window and expired flag.</b> An item enters the list when it is <see cref="ExpiryStatusView.ExpiringSoon"/>
/// against <see cref="WarningWindowDays"/> or — when <see cref="IncludeExpired"/> is set — already
/// <see cref="ExpiryStatusView.Expired"/>. Items with no validity (<see cref="ExpiryStatusView.NotApplicable"/>)
/// and items still comfortably valid (<see cref="ExpiryStatusView.Ok"/>) are never listed. The classification
/// is a faithful mirror of <see cref="ExpiryStatusRule"/>, so the SQL and the C# read model agree on the exact
/// boundary conditions.
/// </para>
/// <para>
/// <b>Days remaining.</b> Validity is stored with month granularity (<c>expiry_year</c> / <c>expiry_month</c>)
/// and the item is valid through the last day of that month, so <c>DaysRemaining</c> is that last valid day
/// minus <c>@Today</c> — zero on the last valid day, negative once expired. It is computed in SQL against the
/// handler-supplied <c>@Today</c> (from <see cref="IClock"/>), never the database clock.
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from
/// the request, and every SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global
/// query filter, so the tenant guard is explicit (defense-in-depth, section 7).
/// </para>
/// </remarks>
public sealed record ListExpiringItemsQuery : PagedQuery<PagedResult<ExpiringItem>>
{
    /// <summary>
    /// Warning window in days: an item is listed when its last valid day falls within this many days of
    /// <c>today</c>. Defaults to the shared 30-day window (<see cref="ExpiryStatusRule.DefaultWarningWindowDays"/>);
    /// the validity job passes 30/15/7. Non-positive values collapse to the default.
    /// </summary>
    public int WarningWindowDays { get; init; } = ExpiryStatusRule.DefaultWarningWindowDays;

    /// <summary>
    /// When true, already-expired items are included alongside those expiring within the window; when false,
    /// only items expiring within the window are returned. Defaults to true (the expiry screen shows both).
    /// </summary>
    public bool IncludeExpired { get; init; } = true;

    /// <summary>Optional storage-location filter; null lists at-risk items of every location.</summary>
    public Guid? StorageLocationId { get; init; }
}

/// <summary>
/// Flat read row for the expiry screen and validity job (card [E4] #30). Enxuto by design: it exposes the
/// item, its storage location, lot, month-granularity validity, the derived status and the signed
/// <see cref="DaysRemaining"/> the UI renders — never the <c>StockItem</c> aggregate or its value objects.
/// </summary>
public sealed record ExpiringItem(
    Guid Id,
    string Name,
    string Category,
    string? LotCode,
    decimal Quantity,
    string Unit,
    int ExpiryYear,
    int ExpiryMonth,
    ExpiryStatusView ExpiryStatus,
    int DaysRemaining,
    bool IsControlled,
    Guid StorageLocationId,
    string? StorageLocationName,
    string? StorageLocationType);

internal sealed class ListExpiringItemsQueryHandler
    : BaseDataAccess, IQueryHandler<ListExpiringItemsQuery, PagedResult<ExpiringItem>>
{
    // The last valid day of (expiry_year, expiry_month), computed once in the CTE and reused by the status
    // CASE, the days-remaining subtraction and the ordering. The status CASE is a faithful mirror of
    // ExpiryStatusRule.Classify: Expired past that day, ExpiringSoon when it is within @WarningWindowDays of
    // @Today, otherwise Ok. Only rows with a validity reach this projection (the WHERE prunes NULL validity),
    // so NotApplicable never appears; the outer WHERE is the SQL form of ExpiryStatusRule.IsAtRisk — it keeps
    // ExpiringSoon and, when @IncludeExpired, Expired. @Today/@WarningWindowDays come from the handler (IClock).
    private const string Sql =
        """
        WITH classified AS (
            SELECT
                v.id,
                v.name,
                v.category,
                v.lot_code,
                v.quantity_amount,
                v.quantity_unit,
                v.expiry_year,
                v.expiry_month,
                v.is_controlled,
                v.storage_location_id,
                v.storage_location_name,
                v.storage_location_type,
                (make_date(v.expiry_year, v.expiry_month, 1)
                 + INTERVAL '1 month' - INTERVAL '1 day')::date AS last_valid_day
            FROM inventory.stock_view AS v
            WHERE v.company_id = @CompanyId
              AND v.expiry_year IS NOT NULL
              AND v.expiry_month IS NOT NULL
              AND (@StorageLocationId IS NULL OR v.storage_location_id = @StorageLocationId)
        ),
        scored AS (
            SELECT
                c.*,
                CASE
                    WHEN @Today > c.last_valid_day THEN @Expired
                    WHEN c.last_valid_day <= (@Today + (@WarningWindowDays || ' days')::interval)::date
                        THEN @ExpiringSoon
                    ELSE @Ok
                END AS expiry_status,
                (c.last_valid_day - @Today) AS days_remaining
            FROM classified AS c
        ),
        records AS (
            SELECT
                s.id,
                s.name,
                s.category,
                s.lot_code,
                s.quantity_amount,
                s.quantity_unit,
                s.expiry_year,
                s.expiry_month,
                s.expiry_status,
                s.days_remaining,
                s.is_controlled,
                s.storage_location_id,
                s.storage_location_name,
                s.storage_location_type,
                ROW_NUMBER() OVER (ORDER BY s.last_valid_day ASC, s.name ASC, s.id ASC) AS row_number,
                COUNT(*)     OVER ()                                                     AS total_rows
            FROM scored AS s
            WHERE s.expiry_status = @ExpiringSoon
               OR (@IncludeExpired AND s.expiry_status = @Expired)
        )
        SELECT
            id,
            name,
            category,
            lot_code              AS lotcode,
            quantity_amount       AS quantity,
            quantity_unit         AS unit,
            expiry_year           AS expiryyear,
            expiry_month          AS expirymonth,
            expiry_status         AS expirystatus,
            days_remaining        AS daysremaining,
            is_controlled         AS iscontrolled,
            storage_location_id   AS storagelocationid,
            storage_location_name AS storagelocationname,
            storage_location_type AS storagelocationtype,
            total_rows
        FROM records
        WHERE row_number BETWEEN @FirstResult AND @LastResult
        ORDER BY row_number;
        """;

    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public ListExpiringItemsQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext,
        IClock clock)
        : base(connectionFactory)
    {
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<PagedResult<ExpiringItem>> HandleAsync(
        ListExpiringItemsQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        ExpiringItemsQueryParameters parameters = BuildParameters(request);

        IReadOnlyList<ExpiringItemRow> rows = (await connection.QueryAsync<ExpiringItemRow>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        int totalCount = rows.Count > 0 ? rows[0].TotalRows : 0;

        IReadOnlyList<ExpiringItem> items = rows
            .Select(row => row.ToExpiringItem())
            .ToList();

        return new PagedResult<ExpiringItem>(items, totalCount, request.Page, request.PageSize);
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes from
    /// <see cref="ITenantContext"/> (never the request), a non-positive window collapses to the shared default,
    /// and <c>@Today</c> is derived from the injected <see cref="IClock"/> — extracted so the tenant guard and
    /// window normalization are unit-testable without a live database.
    /// </summary>
    internal ExpiringItemsQueryParameters BuildParameters(ListExpiringItemsQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        StorageLocationId: request.StorageLocationId,
        Today: DateOnly.FromDateTime(_clock.UtcNow),
        WarningWindowDays: NormalizeWindow(request.WarningWindowDays),
        IncludeExpired: request.IncludeExpired,
        Ok: (int)ExpiryStatusView.Ok,
        ExpiringSoon: (int)ExpiryStatusView.ExpiringSoon,
        Expired: (int)ExpiryStatusView.Expired,
        FirstResult: request.FirstResult,
        LastResult: request.LastResult);

    /// <summary>Falls back to the shared 30-day window when the caller supplies a non-positive value.</summary>
    private static int NormalizeWindow(int windowDays)
        => windowDays > 0 ? windowDays : ExpiryStatusRule.DefaultWarningWindowDays;

    /// <summary>
    /// Dapper materialization row: carries the per-page <c>total_rows</c> (from <c>COUNT(*) OVER()</c>)
    /// alongside the projected columns, so the total and the page come back in a single round-trip.
    /// </summary>
    private sealed record ExpiringItemRow(
        Guid Id,
        string Name,
        string Category,
        string? LotCode,
        decimal Quantity,
        string Unit,
        int ExpiryYear,
        int ExpiryMonth,
        ExpiryStatusView ExpiryStatus,
        int DaysRemaining,
        bool IsControlled,
        Guid StorageLocationId,
        string? StorageLocationName,
        string? StorageLocationType,
        int TotalRows)
    {
        public ExpiringItem ToExpiringItem() => new(
            Id,
            Name,
            Category,
            LotCode,
            Quantity,
            Unit,
            ExpiryYear,
            ExpiryMonth,
            ExpiryStatus,
            DaysRemaining,
            IsControlled,
            StorageLocationId,
            StorageLocationName,
            StorageLocationType);
    }
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="ListExpiringItemsQuery"/>. The property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the
/// tenant guard, window normalization and pagination can be asserted without a live database.
/// </summary>
internal sealed record ExpiringItemsQueryParameters(
    Guid CompanyId,
    Guid? StorageLocationId,
    DateOnly Today,
    int WarningWindowDays,
    bool IncludeExpired,
    int Ok,
    int ExpiringSoon,
    int Expired,
    int FirstResult,
    int LastResult);
