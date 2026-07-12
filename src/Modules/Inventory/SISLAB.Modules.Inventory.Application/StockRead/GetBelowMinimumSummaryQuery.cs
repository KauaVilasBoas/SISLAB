using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Application.StockRead;

/// <summary>
/// Read-side query (card [E4] #32) that counts, for the <b>active company</b>, how many stock items are
/// below their minimum stock — the single "reposição" KPI the dashboard shows and the reposition-alert job
/// keys off. It counts the items of <c>inventory.stock_view</c> whose <c>is_below_minimum</c> flag is set,
/// in a single tenant-scoped round-trip, without pulling the whole list.
/// </summary>
/// <remarks>
/// <para>
/// <b>Same boundary as the list.</b> The count reuses the view's precomputed <c>is_below_minimum</c> column
/// (<c>quantity_amount &lt; minimum_quantity_amount</c>), so this KPI and <see cref="ListItemsBelowMinimumQuery"/>
/// agree exactly on which items count — an item at its minimum is not counted (it is not yet below).
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from
/// the request, and the single SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF
/// global query filter, so the tenant guard is explicit (defense-in-depth, section 7).
/// </para>
/// </remarks>
public sealed record GetBelowMinimumSummaryQuery : IQuery<BelowMinimumSummary>;

/// <summary>
/// The single below-minimum KPI for the active company (card [E4] #32): how many items are currently below
/// their minimum stock and therefore need reposition. Zero means the whole inventory is at or above minimum.
/// </summary>
public sealed record BelowMinimumSummary(int BelowMinimumCount);

internal sealed class GetBelowMinimumSummaryQueryHandler
    : BaseDataAccess, IQueryHandler<GetBelowMinimumSummaryQuery, BelowMinimumSummary>
{
    // COUNT over the precomputed is_below_minimum flag — the same boundary the below-minimum list uses, so
    // the KPI and the list can never disagree. A single row always comes back. company_id keeps the
    // mandatory tenant scoping (the read side has no EF global query filter).
    private const string Sql =
        """
        SELECT COUNT(*)::int AS belowminimumcount
        FROM inventory.stock_view AS v
        WHERE v.company_id = @CompanyId
          AND v.is_below_minimum;
        """;

    private readonly ITenantContext _tenantContext;

    public GetBelowMinimumSummaryQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<BelowMinimumSummary> HandleAsync(
        GetBelowMinimumSummaryQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        BelowMinimumSummaryQueryParameters parameters = BuildParameters();

        return await connection.QuerySingleAsync<BelowMinimumSummary>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Materializes the Dapper parameter set: the company id always comes from <see cref="ITenantContext"/>
    /// (never the request). Extracted so the tenant guard is unit-testable without a live database.
    /// </summary>
    internal BelowMinimumSummaryQueryParameters BuildParameters() => new(
        CompanyId: _tenantContext.CompanyId);
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="GetBelowMinimumSummaryQuery"/>. The property name matches
/// the <c>@Parameter</c> token in the SQL exactly (Dapper binds by name). Exposed to the module's tests so
/// the tenant guard can be asserted without a live database.
/// </summary>
internal sealed record BelowMinimumSummaryQueryParameters(Guid CompanyId);
