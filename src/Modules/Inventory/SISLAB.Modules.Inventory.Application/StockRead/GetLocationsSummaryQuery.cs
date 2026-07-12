using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Application.StockRead;

/// <summary>
/// Read-side query (card [E4] #29) that aggregates the storage locations of the <b>active company</b>
/// for the left column of the inventory master-detail screen (#46): per location its name, type, item
/// count, expired-item count and a "critical" flag (a controlled box). It feeds the per-location badges
/// (including the red expired badge).
/// </summary>
/// <remarks>
/// <para>
/// <b>Source.</b> Unlike the item listing (which reads <c>inventory.stock_view</c>), this aggregation
/// starts from <c>inventory.storage_locations</c> and LEFT JOINs <c>stock_items</c>, so an empty
/// location (count 0) still appears — an empty controlled box is still shown and still flagged critical.
/// The view only carries locations that hold items, hence it is not the right base here.
/// </para>
/// <para>
/// <b>Derived counts.</b> Item and expired counts are read-side derivations, never persisted (decision
/// recorded on the StorageLocation aggregate / card #29). "Expired" reuses the same month-granularity
/// rule as the item listing: the item is expired when <c>@Today</c> is past the last day of
/// (expiry_year, expiry_month).
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The company comes from <see cref="ITenantContext"/>; every table in the join
/// is filtered by <c>company_id = @CompanyId</c> (the read-side has no EF global query filter).
/// </para>
/// <para>
/// Not paginated: the number of storage locations per laboratory is small and the whole column is shown
/// at once, so a flat ordered list is returned rather than a <c>PagedResult</c>.
/// </para>
/// </remarks>
public sealed record GetLocationsSummaryQuery : IQuery<IReadOnlyList<LocationSummaryItem>>;

/// <summary>
/// Per-location aggregate row for the master-detail left column (card [E4] #29). <see cref="IsCritical"/>
/// marks a controlled-storage location (e.g. the controlled box), which the UI highlights.
/// </summary>
public sealed record LocationSummaryItem(
    Guid Id,
    string Name,
    string Type,
    bool IsActive,
    int ItemCount,
    int ExpiredItemCount,
    bool IsCritical);

internal sealed class GetLocationsSummaryQueryHandler
    : BaseDataAccess, IQueryHandler<GetLocationsSummaryQuery, IReadOnlyList<LocationSummaryItem>>
{
    /// <summary>The only location type allowed to hold controlled substances — the "critical" storage.</summary>
    private const string ControlledType = "Controlled";

    // Item and expired-item counts are derived per location. COUNT(si.id) counts only matched items
    // (0 for an empty location, since the LEFT JOIN yields a NULL id). The expired count sums the same
    // last-valid-day rule used by the item listing, evaluated against the handler-supplied @Today.
    private const string Sql =
        """
        SELECT
            sl.id                                        AS id,
            sl.name                                      AS name,
            sl.type                                      AS type,
            sl.is_active                                 AS isactive,
            COUNT(si.id)::int                            AS itemcount,
            COUNT(si.id) FILTER (
                WHERE si.expiry_year IS NOT NULL
                  AND si.expiry_month IS NOT NULL
                  AND @Today > (make_date(si.expiry_year, si.expiry_month, 1)
                                + INTERVAL '1 month' - INTERVAL '1 day')::date
            )::int                                       AS expireditemcount,
            (sl.type = @ControlledType)                  AS iscritical
        FROM inventory.storage_locations AS sl
        LEFT JOIN inventory.stock_items AS si
            ON si.storage_location_id = sl.id
           AND si.company_id = sl.company_id
        WHERE sl.company_id = @CompanyId
        GROUP BY sl.id, sl.name, sl.type, sl.is_active
        ORDER BY sl.name ASC;
        """;

    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public GetLocationsSummaryQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext,
        IClock clock)
        : base(connectionFactory)
    {
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<IReadOnlyList<LocationSummaryItem>> HandleAsync(
        GetLocationsSummaryQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        LocationsSummaryQueryParameters parameters = BuildParameters();

        IReadOnlyList<LocationSummaryItem> items = (await connection.QueryAsync<LocationSummaryItem>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        return items;
    }

    /// <summary>
    /// Materializes the Dapper parameter set: the company id always comes from <see cref="ITenantContext"/>
    /// (never the request) and <c>@Today</c> from the injected <see cref="IClock"/>. Extracted so the
    /// tenant guard is unit-testable without a live database.
    /// </summary>
    internal LocationsSummaryQueryParameters BuildParameters() => new(
        CompanyId: _tenantContext.CompanyId,
        Today: DateOnly.FromDateTime(_clock.UtcNow),
        ControlledType: ControlledType);
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="GetLocationsSummaryQuery"/>. Property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so
/// the tenant guard can be asserted without a live database.
/// </summary>
internal sealed record LocationsSummaryQueryParameters(
    Guid CompanyId,
    DateOnly Today,
    string ControlledType);
