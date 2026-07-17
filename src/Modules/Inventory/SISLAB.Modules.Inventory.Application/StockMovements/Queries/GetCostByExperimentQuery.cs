using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Application.StockMovements.Queries;

/// <summary>
/// Read-side query (card [E4] #109) that builds the <b>cost-by-experiment</b> report of the <b>active
/// company</b>: how much money was spent on consumption per experiment, top <see cref="Top"/> experiments by
/// spend. It reads the projected ledger <c>inventory.stock_movements</c> (card [E4] #33) via Dapper — never
/// the write DbContext — keeping only priced consumption rows (<c>movement_type = 'Consumed'</c> and
/// <c>unit_cost_brl IS NOT NULL</c>), valuing each as <c>quantity × unit_cost_brl</c> (the real per-batch
/// cost the FEFO projection recorded).
/// </summary>
/// <remarks>
/// <para>
/// <b>Consumptions with no experiment.</b> They are aggregated into a single "no experiment" bucket
/// (<see cref="ExperimentCostItem.ExperimentId"/> == <see langword="null"/>) — the "consumos sem experimento
/// no total geral" acceptance criterion — so the report still accounts for every priced consumption. The UI
/// labels the null bucket ("Sem experimento").
/// </para>
/// <para>
/// <b>Experiment identity.</b> The experiment is a cross-module reference held <b>by value</b> (Guid); the
/// Experiment module is fase 2, so there is no name to join yet — the report returns the id and the UI shows a
/// short form until the module lands. Ordering is by spend descending, capped to the top <see cref="Top"/>.
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The company comes from <see cref="ITenantContext"/> (never the request); the SELECT
/// keeps <c>WHERE company_id = @CompanyId</c> (defense-in-depth, section 7).
/// </para>
/// </remarks>
public sealed record GetCostByExperimentQuery : IQuery<IReadOnlyList<ExperimentCostItem>>
{
    /// <summary>Default number of top-spend experiments returned.</summary>
    public const int DefaultTop = 10;

    /// <summary>How many top-spend experiments to return; clamped by the handler to a sane range.</summary>
    public int Top { get; init; } = DefaultTop;
}

/// <summary>
/// One experiment's consumption cost for the active company (card [E4] #109). <see cref="ExperimentId"/> is
/// null for the aggregated "no experiment" bucket; <see cref="TotalCost"/> is the summed BRL cost of its
/// priced consumptions.
/// </summary>
public sealed record ExperimentCostItem(Guid? ExperimentId, decimal TotalCost);

internal sealed class GetCostByExperimentQueryHandler
    : BaseDataAccess, IQueryHandler<GetCostByExperimentQuery, IReadOnlyList<ExperimentCostItem>>
{
    private const string ConsumedMovementType = "Consumed";
    private const int MinTop = 1;
    private const int MaxTop = 50;

    // Priced consumption rows only, valued at quantity × unit_cost_brl, grouped by experiment_id (NULL folds
    // into a single "no experiment" bucket), highest spend first, capped to @Top. company_id keeps the
    // mandatory tenant scoping.
    private const string Sql =
        """
        SELECT
            m.experiment_id                          AS experimentid,
            SUM(m.quantity_amount * m.unit_cost_brl) AS totalcost
        FROM inventory.stock_movements AS m
        WHERE m.company_id = @CompanyId
          AND m.movement_type = @ConsumedMovementType
          AND m.unit_cost_brl IS NOT NULL
        GROUP BY m.experiment_id
        ORDER BY totalcost DESC
        LIMIT @Top;
        """;

    private readonly ITenantContext _tenantContext;

    public GetCostByExperimentQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<ExperimentCostItem>> HandleAsync(
        GetCostByExperimentQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        CostByExperimentQueryParameters parameters = BuildParameters(request);

        IReadOnlyList<ExperimentCostItem> experiments = (await connection.QueryAsync<ExperimentCostItem>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        return experiments;
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes from
    /// <see cref="ITenantContext"/> (never the request), the consumption discriminator is pinned, and the top
    /// count is clamped to <c>[1, 50]</c> — extracted so the tenant guard and clamping are unit-testable
    /// without a live database.
    /// </summary>
    internal CostByExperimentQueryParameters BuildParameters(GetCostByExperimentQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        ConsumedMovementType: ConsumedMovementType,
        Top: Math.Clamp(request.Top, MinTop, MaxTop));
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="GetCostByExperimentQuery"/>. The property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the
/// tenant guard and clamping can be asserted without a live database.
/// </summary>
internal sealed record CostByExperimentQueryParameters(
    Guid CompanyId,
    string ConsumedMovementType,
    int Top);
