using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Application.StockMovements.Queries;

/// <summary>
/// Read-side query (card [E7] #47) that lists the <b>most recent stock movements of the active company across
/// every item</b> — entries, consumptions, transfers and disposals — for the inventory page's "recent activity"
/// panel. Unlike <see cref="ListStockMovementsQuery"/> (the per-item ledger, pinned to one
/// <c>stock_item_id</c>), this is a cross-item feed: it never fixes an item, capped to the latest
/// <see cref="Top"/> rows. It reads the projected <c>inventory.stock_movements</c> table (one row per movement,
/// fed asynchronously from the Outbox) via Dapper — never the write DbContext — and joins
/// <c>inventory.stock_items</c> to bring the item's <c>name</c> (the ledger stores neither name nor notes),
/// projecting the flat <see cref="RecentMovementItem"/> the panel renders.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from the
/// request, and every SELECT (the ledger and the joined <c>stock_items</c>) keeps
/// <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global query filter, so the tenant guard is
/// explicit (defense-in-depth, section 7).
/// </para>
/// <para>
/// <b>Not paginated by design.</b> This feeds a small, fixed "últimas N movimentações" panel, so it takes a
/// simple <see cref="Top"/> cap (<c>LIMIT</c>) rather than the <see cref="PagedQuery{T}"/> window used by the
/// per-item ledger. <see cref="Top"/> is clamped to a sane range by the handler.
/// </para>
/// <para>
/// <b>Ordering.</b> Rows are ordered by <c>occurred_on</c> descending (the operator's business date, which is
/// nullable — nulls sort last), with <c>created_at_utc</c> descending as a stable tie-breaker so movements
/// sharing a date keep the order in which they were projected, giving a deterministic feed.
/// </para>
/// <para>
/// <b>notes.</b> The read model has no notes column yet (mirrors <see cref="ListStockMovementsQuery"/>), so
/// <see cref="RecentMovementItem.Notes"/> is projected as null to keep the row shape stable for the UI; the
/// audit trail (E9) may surface it later.
/// </para>
/// </remarks>
public sealed record ListRecentMovementsQuery : IQuery<IReadOnlyList<RecentMovementItem>>
{
    /// <summary>Default number of recent movements returned when the caller does not specify one.</summary>
    public const int DefaultTop = 20;

    /// <summary>Upper bound on <see cref="Top"/>, guarding the panel query against an unbounded scan.</summary>
    public const int MaxTop = 100;

    /// <summary>
    /// How many of the latest movements to return. Clamped by the handler to <c>[1, <see cref="MaxTop"/>]</c>;
    /// defaults to <see cref="DefaultTop"/>.
    /// </summary>
    public int Top { get; init; } = DefaultTop;
}

/// <summary>
/// Flat read row for the cross-item recent-movements panel (card [E7] #47). Enxuto by design: it exposes the
/// primitives the panel renders directly — including the item's <see cref="StockItemName"/> (joined from
/// <c>stock_items</c>) — and never leaks the <c>StockItem</c> aggregate or its value objects. <see cref="Type"/>
/// is the movement discriminator as a string (<c>Received</c>, <c>Consumed</c>, <c>Transferred</c>,
/// <c>Disposed</c>); <see cref="Notes"/> is null while the read model has no notes column.
/// </summary>
/// <remarks>
/// <b>EstimatedCostBrl (card #110).</b> The valued cost of the movement in BRL — <c>quantity × unit_cost_brl</c>
/// of the batch it was charged against — or <c>null</c> when the movement carries no cost (entries/transfers
/// without a price, or draws from an unpriced batch). Since the projection appends one row per batch allocation,
/// this is the cost of that single allocation, matching the row the panel renders. It is gestão-sensitive data:
/// the API exposes it unconditionally on this <c>[Authorize]</c> read (batch balances/costs already surface on
/// the movement forms), and the SPA gates its DISPLAY behind <c>Inventory.Cost.Read</c>.
/// </remarks>
public sealed record RecentMovementItem(
    Guid Id,
    Guid StockItemId,
    string StockItemName,
    string Type,
    decimal Quantity,
    string Unit,
    DateOnly? OccurredOn,
    string? Notes,
    decimal? EstimatedCostBrl);

internal sealed class ListRecentMovementsQueryHandler
    : BaseDataAccess, IQueryHandler<ListRecentMovementsQuery, IReadOnlyList<RecentMovementItem>>
{
    // Cross-item feed of the active company's latest movements. The tenant-scoped JOIN to stock_items brings
    // the item name (the ledger stores none). Ordering is occurred_on DESC (NULLS LAST, since occurred_on is
    // nullable) with created_at_utc DESC as a stable tie-breaker, so same-date movements keep projection order
    // and the feed is deterministic. company_id keeps the mandatory tenant scoping. LIMIT @Top caps the panel;
    // notes has no column in the read model yet, so it is projected as NULL to keep the row shape stable.
    private const string Sql =
        """
        SELECT
            m.id             AS id,
            m.stock_item_id  AS stockitemid,
            si.name          AS stockitemname,
            m.movement_type  AS type,
            m.quantity_amount AS quantity,
            m.quantity_unit  AS unit,
            m.occurred_on    AS occurredon,
            NULL             AS notes,
            (m.quantity_amount * m.unit_cost_brl) AS estimatedcostbrl
        FROM inventory.stock_movements AS m
        JOIN inventory.stock_items AS si
            ON si.id = m.stock_item_id
           AND si.company_id = m.company_id
        WHERE m.company_id = @CompanyId
        ORDER BY m.occurred_on DESC NULLS LAST, m.created_at_utc DESC
        LIMIT @Top;
        """;

    private readonly ITenantContext _tenantContext;

    public ListRecentMovementsQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<RecentMovementItem>> HandleAsync(
        ListRecentMovementsQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        RecentMovementsQueryParameters parameters = BuildParameters(request);

        return (await connection.QueryAsync<RecentMovementItem>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes from
    /// <see cref="ITenantContext"/> (never the request), and <see cref="ListRecentMovementsQuery.Top"/> is
    /// clamped to <c>[1, <see cref="ListRecentMovementsQuery.MaxTop"/>]</c> so a malformed cap can neither
    /// return nothing nor trigger an unbounded scan. Extracted so the tenant guard and the clamp are
    /// unit-testable without a live database.
    /// </summary>
    internal RecentMovementsQueryParameters BuildParameters(ListRecentMovementsQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        Top: Math.Clamp(request.Top, 1, ListRecentMovementsQuery.MaxTop));
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="ListRecentMovementsQuery"/>. The property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the
/// tenant guard and the <see cref="Top"/> clamp can be asserted without a live database.
/// </summary>
internal sealed record RecentMovementsQueryParameters(
    Guid CompanyId,
    int Top);
