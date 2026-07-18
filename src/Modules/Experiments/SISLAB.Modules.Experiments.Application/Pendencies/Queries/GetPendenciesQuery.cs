using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Pendencies.Queries;

/// <summary>
/// Read-side query (card [E11] #90) that assembles the operator's "pendencies" panel for the active company: the
/// open work that still needs a human action across the Experiments module. It unions three sources over Dapper —
/// never the write DbContext — into one uniform, flat list the panel renders:
/// <list type="number">
///   <item>experiments <b>awaiting calculation</b> (in vivo hand-off: data launched, calc not yet run);</item>
///   <item>experiment <b>steps not yet performed</b> on a non-terminal experiment (pending/in-progress work);</item>
///   <item>biobank <b>samples with a remaining balance</b> that have no completed analysis yet.</item>
/// </list>
/// No new aggregate: the panel is a pure projection over the existing write tables.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> Every branch of the union keeps <c>WHERE company_id = @CompanyId</c> (steps join back to
/// the tenant-checked experiment), and the company id comes from <see cref="ITenantContext"/> — never the request.
/// The read side has no EF global query filter, so the tenant guard is explicit (defense-in-depth, section 7).
/// Ordered oldest-first so the most overdue pendency surfaces at the top.
/// </remarks>
public sealed record GetPendenciesQuery : IQuery<PendenciesResult>;

/// <summary>The pendencies panel: the flat list of open items plus per-kind counts for the summary badges.</summary>
public sealed record PendenciesResult(
    IReadOnlyList<PendencyItem> Items,
    int AwaitingCalculationCount,
    int PendingStepCount,
    int SampleAwaitingAnalysisCount);

/// <summary>One open item on the pendencies panel; the <see cref="Kind"/> tells the UI how to route the action.</summary>
public sealed record PendencyItem(
    string Kind,
    Guid ReferenceId,
    string Title,
    string Detail,
    DateTime SinceUtc);

internal sealed class GetPendenciesQueryHandler
    : BaseDataAccess, IQueryHandler<GetPendenciesQuery, PendenciesResult>
{
    /// <summary>The three kinds, materialized as literals in the union so the UI can switch on a stable value.</summary>
    private const string AwaitingCalculationKind = "AwaitingCalculation";
    private const string PendingStepKind = "PendingStep";
    private const string SampleAwaitingAnalysisKind = "SampleAwaitingAnalysis";

    // Three tenant-scoped branches unioned into one shaped list. A step is "pending" when it has not been
    // performed and its experiment is still non-terminal (not completed/archived). A sample is "awaiting
    // analysis" when it still has a positive derived balance and no completed analysis has been recorded yet.
    private const string Sql =
        """
        SELECT * FROM (
            SELECT
                'AwaitingCalculation' AS kind,
                e.id                  AS referenceid,
                e.title               AS title,
                'Aguardando cálculo'  AS detail,
                e.created_at_utc      AS sinceutc
            FROM experiments.experiments AS e
            WHERE e.company_id = @CompanyId
              AND e.status = 'AwaitingCalculation'

            UNION ALL

            SELECT
                'PendingStep' AS kind,
                e.id          AS referenceid,
                e.title       AS title,
                s.title       AS detail,
                e.created_at_utc AS sinceutc
            FROM experiments.experiment_steps AS s
            INNER JOIN experiments.experiments AS e ON e.id = s.experiment_id
            WHERE e.company_id = @CompanyId
              AND s.performed_at_utc IS NULL
              AND e.status NOT IN ('Completed', 'Archived')

            UNION ALL

            SELECT
                'SampleAwaitingAnalysis' AS kind,
                sa.id                    AS referenceid,
                sa.code                  AS title,
                'Amostra com saldo pendente de análise' AS detail,
                sa.collected_at_utc      AS sinceutc
            FROM experiments.samples AS sa
            WHERE sa.company_id = @CompanyId
              AND sa.collected_value > COALESCE(
                    (SELECT SUM(a.consumed_value)
                     FROM experiments.sample_analyses a
                     WHERE a.sample_id = sa.id), 0)
              AND NOT EXISTS (
                    SELECT 1 FROM experiments.sample_analyses a
                    WHERE a.sample_id = sa.id AND a.status = 'Completed')
        ) AS pendencies
        ORDER BY sinceutc ASC, title ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public GetPendenciesQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<PendenciesResult> HandleAsync(
        GetPendenciesQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        PendenciesQueryParameters parameters = BuildParameters();

        IReadOnlyList<PendencyItem> items = (await connection.QueryAsync<PendencyItem>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        return Assemble(items);
    }

    /// <summary>
    /// Shapes the flat union rows into the panel result, deriving the per-kind counts. Pure and side-effect free,
    /// so the badge counts and grouping are unit-testable without a live database.
    /// </summary>
    internal static PendenciesResult Assemble(IReadOnlyList<PendencyItem> items) => new(
        items,
        AwaitingCalculationCount: items.Count(item => item.Kind == AwaitingCalculationKind),
        PendingStepCount: items.Count(item => item.Kind == PendingStepKind),
        SampleAwaitingAnalysisCount: items.Count(item => item.Kind == SampleAwaitingAnalysisKind));

    /// <summary>
    /// Materializes the Dapper parameter set. The company id always comes from <see cref="ITenantContext"/>
    /// (never the request). Extracted so the tenant guard is unit-testable without a live database.
    /// </summary>
    internal PendenciesQueryParameters BuildParameters() => new(_tenantContext.CompanyId);
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="GetPendenciesQuery"/>. Property name matches the
/// <c>@CompanyId</c> token exactly (Dapper binds by name). Exposed to the module's tests so the tenant guard can
/// be asserted without a live database.
/// </summary>
internal sealed record PendenciesQueryParameters(Guid CompanyId);
