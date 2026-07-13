using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Application.StockMovements.Queries;

/// <summary>
/// Read-side query (card [E5] #35) that loads the single stock item of the <b>active company</b>
/// identified by <see cref="StockItemId"/>, or <see langword="null"/> when no such item exists for that
/// company. It reads the desnormalized <c>inventory.stock_view</c> (item × storage location, card [E4]
/// #33) via Dapper — never the write DbContext — and projects the flat <see cref="StockItemDetail"/> the
/// module's public boundary (<c>IInventoryApi</c>) hands to other modules.
/// </summary>
/// <remarks>
/// <para>
/// This is the by-id read the E4 listing queries did not cover: the public API's
/// <c>GetStockItemAsync</c> / <c>StockItemExistsAsync</c> / <c>GetOnHandBalanceAsync</c> all resolve one
/// item, so they share this single round-trip rather than each carrying their own SQL. Existence is
/// "the query returned a row"; the balance is a projection of the same row.
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never
/// from the request, and the SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF
/// global query filter, so the tenant guard is explicit (defense-in-depth, section 7). An id belonging
/// to another company is indistinguishable from a non-existent one: both yield <see langword="null"/>.
/// </para>
/// </remarks>
public sealed record GetStockItemDetailQuery(Guid StockItemId) : IQuery<StockItemDetail?>;

/// <summary>
/// Flat read row for a single stock item (card [E5] #35). Enxuto by design: it exposes the primitives the
/// public boundary needs — item, category, current and minimum quantity with their units, month-granularity
/// validity, storage location and controlled flag — never the <c>StockItem</c> aggregate or its value objects.
/// </summary>
public sealed record StockItemDetail(
    Guid Id,
    string Name,
    string Category,
    decimal Quantity,
    string Unit,
    decimal MinimumQuantity,
    string MinimumUnit,
    int? ExpiryYear,
    int? ExpiryMonth,
    Guid StorageLocationId,
    string? StorageLocationName,
    bool IsControlled,
    Guid CompanyId);

internal sealed class GetStockItemDetailQueryHandler
    : BaseDataAccess, IQueryHandler<GetStockItemDetailQuery, StockItemDetail?>
{
    // Single-row lookup by (company_id, id): no pagination window is needed. company_id keeps the
    // mandatory tenant scoping, so an id from another tenant returns no row (→ null), exactly like a
    // missing id. The columns are aliased to the StockItemDetail property names (Dapper binds by name).
    private const string Sql =
        """
        SELECT
            v.id                       AS id,
            v.name                     AS name,
            v.category                 AS category,
            v.quantity_amount          AS quantity,
            v.quantity_unit            AS unit,
            v.minimum_quantity_amount  AS minimumquantity,
            v.minimum_quantity_unit    AS minimumunit,
            v.expiry_year              AS expiryyear,
            v.expiry_month             AS expirymonth,
            v.storage_location_id      AS storagelocationid,
            v.storage_location_name    AS storagelocationname,
            v.is_controlled            AS iscontrolled,
            v.company_id               AS companyid
        FROM inventory.stock_view AS v
        WHERE v.company_id = @CompanyId
          AND v.id = @StockItemId;
        """;

    private readonly ITenantContext _tenantContext;

    public GetStockItemDetailQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<StockItemDetail?> HandleAsync(
        GetStockItemDetailQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        StockItemDetailParameters parameters = BuildParameters(request);

        return await connection.QuerySingleOrDefaultAsync<StockItemDetail>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes
    /// from <see cref="ITenantContext"/> (never the request) — extracted so the tenant guard is
    /// unit-testable without a live database.
    /// </summary>
    internal StockItemDetailParameters BuildParameters(GetStockItemDetailQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        StockItemId: request.StockItemId);
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="GetStockItemDetailQuery"/>. The property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the
/// tenant guard can be asserted without a live database.
/// </summary>
internal sealed record StockItemDetailParameters(
    Guid CompanyId,
    Guid StockItemId);
