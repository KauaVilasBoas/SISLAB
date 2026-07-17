using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Application.StorageLocations.Queries;

/// <summary>
/// Read-side query (card [E7] #112) that lists the <b>full storage locations of the active company</b> for the
/// management screen: every location, active or not, with its editable metadata (name, type, description) and,
/// for a refrigerated one, its target temperature range, plus a derived <c>ItemCount</c> so the UI can warn
/// before deactivating a location that still holds stock. Unlike <c>GetLocationsSummaryQuery</c> (the
/// master-detail left column, which also derives the expired count and a "critical" flag for the item
/// browser), this is the flat gestão listing: it exposes the write-side fields the edit form binds to, not the
/// browsing badges.
/// </summary>
/// <remarks>
/// <para>
/// <b>Source.</b> Starts from <c>inventory.storage_locations</c> and LEFT JOINs <c>stock_items</c> so an empty
/// location still appears with a count of 0 — the management list must show every location, including a
/// brand-new empty one.
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The company comes from <see cref="ITenantContext"/> (never the request); every table
/// in the join is filtered by <c>company_id = @CompanyId</c> — the read-side has no EF global query filter, so
/// the tenant guard is explicit (defense-in-depth, section 7).
/// </para>
/// <para>
/// Not paginated: a laboratory has few storage locations and the whole list is shown at once, so a flat list
/// ordered by name is returned rather than a <c>PagedResult</c> — mirroring the summary query.
/// </para>
/// </remarks>
public sealed record GetStorageLocationsQuery : IQuery<IReadOnlyList<StorageLocationListItem>>;

/// <summary>
/// Flat management row for a storage location (card [E7] #112). Exposes the write-side editable fields the
/// gestão form binds to — <see cref="Name"/>, <see cref="Type"/> (as a string discriminator), the optional
/// <see cref="Description"/> and, for a refrigerated location, the <see cref="TemperatureMinCelsius"/>/
/// <see cref="TemperatureMaxCelsius"/> bounds — plus the current <see cref="IsActive"/> flag and the derived
/// <see cref="ItemCount"/>. It never leaks the <c>StorageLocation</c> aggregate or its value objects.
/// </summary>
public sealed record StorageLocationListItem(
    Guid Id,
    string Name,
    string Type,
    string? Description,
    bool IsActive,
    decimal? TemperatureMinCelsius,
    decimal? TemperatureMaxCelsius,
    int ItemCount);

internal sealed class GetStorageLocationsQueryHandler
    : BaseDataAccess, IQueryHandler<GetStorageLocationsQuery, IReadOnlyList<StorageLocationListItem>>
{
    // Full management listing of the active company's storage locations. The LEFT JOIN keeps empty locations
    // (COUNT(si.id) yields 0 for the NULL side) so a brand-new location is still listed. Both temperature
    // bounds are null unless the location is refrigerated (whole-owned null reference on the write side).
    // company_id keeps the mandatory tenant scoping; ordered by name for a stable list.
    private const string Sql =
        """
        SELECT
            sl.id            AS id,
            sl.name          AS name,
            sl.type          AS type,
            sl.description   AS description,
            sl.is_active     AS isactive,
            sl.temp_min      AS temperaturemincelsius,
            sl.temp_max      AS temperaturemaxcelsius,
            COUNT(si.id)::int AS itemcount
        FROM inventory.storage_locations AS sl
        LEFT JOIN inventory.stock_items AS si
            ON si.storage_location_id = sl.id
           AND si.company_id = sl.company_id
        WHERE sl.company_id = @CompanyId
        GROUP BY sl.id, sl.name, sl.type, sl.description, sl.is_active, sl.temp_min, sl.temp_max
        ORDER BY sl.name ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public GetStorageLocationsQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<StorageLocationListItem>> HandleAsync(
        GetStorageLocationsQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        StorageLocationsQueryParameters parameters = BuildParameters();

        return (await connection.QueryAsync<StorageLocationListItem>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();
    }

    /// <summary>
    /// Materializes the Dapper parameter set: the company id always comes from <see cref="ITenantContext"/>,
    /// never the request. Extracted so the tenant guard is unit-testable without a live database.
    /// </summary>
    internal StorageLocationsQueryParameters BuildParameters() => new(CompanyId: _tenantContext.CompanyId);
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="GetStorageLocationsQuery"/>. The property name matches the
/// <c>@Parameter</c> token in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the
/// tenant guard can be asserted without a live database.
/// </summary>
internal sealed record StorageLocationsQueryParameters(Guid CompanyId);
