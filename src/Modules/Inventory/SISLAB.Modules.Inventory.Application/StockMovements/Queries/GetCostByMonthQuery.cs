using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Application.StockMovements.Queries;

/// <summary>
/// Read-side query (card [E4] #109) that builds the <b>cost-by-month</b> report of the <b>active company</b>:
/// how much money was spent on consumption in each of the last <see cref="MonthCount"/> months. It reads the
/// projected ledger <c>inventory.stock_movements</c> (card [E4] #33) via Dapper — never the write DbContext —
/// keeping only consumption rows that carry a unit cost (<c>movement_type = 'Consumed'</c> and
/// <c>unit_cost_brl IS NOT NULL</c>), and values each row as <c>quantity × unit_cost_brl</c> — the real
/// per-batch cost the FEFO projection recorded (one costed row per batch allocation).
/// </summary>
/// <remarks>
/// <para>
/// <b>Unpriced consumptions.</b> Consumptions drawn from donation / no-invoice batches have no
/// <c>unit_cost_brl</c> and are excluded from the total (never counted as zero, which would understate the
/// average) — the "items without a price are handled and signalled, without breaking the total" acceptance
/// criterion. The UI signals separately that some consumption is unpriced.
/// </para>
/// <para>
/// <b>Grain and ordering.</b> One row per calendar month (<c>date_trunc('month', occurred_on)</c>), most
/// recent first, capped to the last <see cref="MonthCount"/> months. BRL is the only currency in the MVP, so
/// the total is a single amount per month (no per-unit split — cost is already money).
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The company comes from <see cref="ITenantContext"/> (never the request); the SELECT
/// keeps <c>WHERE company_id = @CompanyId</c> — the read side has no EF global query filter (defense-in-depth,
/// section 7).
/// </para>
/// </remarks>
public sealed record GetCostByMonthQuery : IQuery<IReadOnlyList<MonthlyCostItem>>
{
    /// <summary>Default number of trailing months returned (a year of history).</summary>
    public const int DefaultMonthCount = 12;

    /// <summary>How many trailing months to return; clamped by the handler to a sane range.</summary>
    public int MonthCount { get; init; } = DefaultMonthCount;
}

/// <summary>
/// One month of consumption cost for the active company (card [E4] #109). <see cref="Month"/> is the first day
/// of the month (UTC date); <see cref="TotalCost"/> is the summed BRL cost of the priced consumptions in it.
/// </summary>
public sealed record MonthlyCostItem(DateOnly Month, decimal TotalCost);

internal sealed class GetCostByMonthQueryHandler
    : BaseDataAccess, IQueryHandler<GetCostByMonthQuery, IReadOnlyList<MonthlyCostItem>>
{
    private const string ConsumedMovementType = "Consumed";
    private const int MinMonthCount = 1;
    private const int MaxMonthCount = 60;

    // Priced consumption rows only, valued at quantity × unit_cost_brl (the FEFO projection already wrote one
    // costed row per batch allocation, so the SUM is the real spend), grouped by calendar month, newest first,
    // capped to @MonthCount. company_id keeps the mandatory tenant scoping.
    private const string Sql =
        """
        SELECT
            date_trunc('month', m.occurred_on)::date AS month,
            SUM(m.quantity_amount * m.unit_cost_brl) AS totalcost
        FROM inventory.stock_movements AS m
        WHERE m.company_id = @CompanyId
          AND m.movement_type = @ConsumedMovementType
          AND m.unit_cost_brl IS NOT NULL
        GROUP BY 1
        ORDER BY 1 DESC
        LIMIT @MonthCount;
        """;

    private readonly ITenantContext _tenantContext;

    public GetCostByMonthQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<MonthlyCostItem>> HandleAsync(
        GetCostByMonthQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        CostByMonthQueryParameters parameters = BuildParameters(request);

        IReadOnlyList<MonthlyCostItem> months = (await connection.QueryAsync<MonthlyCostItem>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        return months;
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes from
    /// <see cref="ITenantContext"/> (never the request), the consumption discriminator is pinned, and the
    /// month count is clamped to <c>[1, 60]</c> — extracted so the tenant guard and clamping are unit-testable
    /// without a live database.
    /// </summary>
    internal CostByMonthQueryParameters BuildParameters(GetCostByMonthQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        ConsumedMovementType: ConsumedMovementType,
        MonthCount: Math.Clamp(request.MonthCount, MinMonthCount, MaxMonthCount));
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="GetCostByMonthQuery"/>. The property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the
/// tenant guard and clamping can be asserted without a live database.
/// </summary>
internal sealed record CostByMonthQueryParameters(
    Guid CompanyId,
    string ConsumedMovementType,
    int MonthCount);
