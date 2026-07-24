using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Configuration.Contracts;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Collection.Queries;

/// <summary>
/// Read-side query (SISLAB-08) that builds the collection status board for a batch: for each planned analysis of each
/// sample type in the plan, the <b>derived</b> pending/completed counts taken from the biobank's <i>real</i> analyses,
/// plus the responsible member. The board never keeps its own status — it is computed by matching each planned analysis,
/// by name and sample type, to the actual <c>experiments.sample_analyses</c> rows of the batch, so it always reflects
/// the true biobank state and can never drift out of sync (the SISLAB-08 "derived status" acceptance criterion).
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from the request,
/// and every SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global query filter, so the
/// tenant guard is explicit (defense-in-depth, section 7).
/// </para>
/// <para>
/// <b>Responsible resolution.</b> The board attaches the member responsible for a sample type by matching, best-effort,
/// a cadastered collection role whose name equals the sample type (e.g. the "Sangue" role) to the plan's role
/// assignment — read across the boundary through the Configuration <see cref="ILabConfiguration"/> port (Contracts only,
/// module isolation, section 2). A sample type with no same-named role simply has no responsible on the board.
/// </para>
/// </remarks>
public sealed record GetCollectionStatusBoardQuery(Guid BatchId) : IQuery<CollectionStatusBoardView>;

/// <summary>The status board: the batch it is for and one row per planned analysis of each sample type.</summary>
public sealed record CollectionStatusBoardView(Guid BatchId, IReadOnlyList<CollectionStatusRow> Rows);

/// <summary>
/// One board row — a planned analysis of a sample type — with the counts derived from the real biobank analyses and the
/// responsible member (when a same-named role is assigned).
/// </summary>
public sealed record CollectionStatusRow(
    string SampleType,
    string AnalysisName,
    Guid? ResponsibleUserId,
    int CollectedSamples,
    int PendingAnalyses,
    int CompletedAnalyses)
{
    /// <summary>True when every real analysis for this planned analysis has been signed off (and at least one exists).</summary>
    public bool IsDone => PendingAnalyses == 0 && CompletedAnalyses > 0;
}

internal sealed class GetCollectionStatusBoardQueryHandler
    : BaseDataAccess, IQueryHandler<GetCollectionStatusBoardQuery, CollectionStatusBoardView>
{
    // The plan header (guards tenant + existence) — the routings/planned analyses come next, and the real analyses are
    // matched by sample type + name against the batch's collected samples.
    internal const string PlanSql =
        """
        SELECT p.id
        FROM experiments.collection_plans AS p
        WHERE p.company_id = @CompanyId AND p.batch_id = @BatchId;
        """;

    // Every planned (sample_type, analysis name) pair of the plan.
    private const string PlannedSql =
        """
        SELECT r.sample_type AS sampletype, a.name AS analysisname
        FROM experiments.collection_planned_analyses AS a
        INNER JOIN experiments.collection_sample_routings AS r ON r.id = a.routing_id
        WHERE r.plan_id = @PlanId
        ORDER BY r.sample_type ASC, a.name ASC;
        """;

    // The batch's real biobank facts, grouped by (sample_type, analysis name): how many samples of the type were
    // collected, and how many analyses of that name are pending vs completed. company_id keeps the tenant scoping.
    internal const string RealSql =
        """
        SELECT
            s.type AS sampletype,
            a.name AS analysisname,
            COUNT(DISTINCT s.id)::int AS collectedsamples,
            COUNT(*) FILTER (WHERE a.status = 'Pending')::int   AS pendinganalyses,
            COUNT(*) FILTER (WHERE a.status = 'Completed')::int AS completedanalyses
        FROM experiments.samples AS s
        INNER JOIN experiments.sample_analyses AS a ON a.sample_id = s.id
        WHERE s.company_id = @CompanyId AND s.batch_id = @BatchId
        GROUP BY s.type, a.name;
        """;

    // Distinct sample types collected for the batch (so a planned type with no analyses yet still shows its samples).
    private const string CollectedByTypeSql =
        """
        SELECT s.type AS sampletype, COUNT(*)::int AS collectedsamples
        FROM experiments.samples AS s
        WHERE s.company_id = @CompanyId AND s.batch_id = @BatchId
        GROUP BY s.type;
        """;

    private readonly ITenantContext _tenantContext;
    private readonly ILabConfiguration _labConfiguration;

    public GetCollectionStatusBoardQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext,
        ILabConfiguration labConfiguration)
        : base(connectionFactory)
    {
        _tenantContext = tenantContext;
        _labConfiguration = labConfiguration;
    }

    public async Task<CollectionStatusBoardView> HandleAsync(
        GetCollectionStatusBoardQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        Guid? planId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                PlanSql,
                new { _tenantContext.CompanyId, request.BatchId },
                cancellationToken: cancellationToken));

        if (planId is null)
            throw new NotFoundException($"No collection plan exists for batch '{request.BatchId}'.");

        IReadOnlyList<PlannedRow> planned = (await connection.QueryAsync<PlannedRow>(
                new CommandDefinition(PlannedSql, new { PlanId = planId.Value }, cancellationToken: cancellationToken)))
            .AsList();

        var realFacts = (await connection.QueryAsync<RealFactRow>(
                new CommandDefinition(
                    RealSql,
                    new { _tenantContext.CompanyId, request.BatchId },
                    cancellationToken: cancellationToken)))
            .ToDictionary(row => (row.SampleType, row.AnalysisName), StringPairComparer.Instance);

        var collectedByType = (await connection.QueryAsync<CollectedByTypeRow>(
                new CommandDefinition(
                    CollectedByTypeSql,
                    new { _tenantContext.CompanyId, request.BatchId },
                    cancellationToken: cancellationToken)))
            .ToDictionary(row => row.SampleType, row => row.CollectedSamples, StringComparer.OrdinalIgnoreCase);

        IReadOnlyDictionary<string, Guid> responsibleBySampleType =
            await ResolveResponsiblesBySampleTypeAsync(planId.Value, connection, cancellationToken);

        IReadOnlyList<CollectionStatusRow> rows =
            ComposeRows(planned, realFacts, collectedByType, responsibleBySampleType);

        return new CollectionStatusBoardView(request.BatchId, rows);
    }

    /// <summary>
    /// Composes the board rows by matching each planned (sample type, analysis) pair to the batch's real biobank facts,
    /// carrying the derived pending/completed counts and the resolved responsible. Pure and side-effect-free — extracted
    /// so the "status derived from the real analyses" rule (the SISLAB-08 acceptance criterion) is unit-testable without
    /// a live database. A planned analysis with no real match yet shows zero pending/completed (and its type's collected
    /// count when known), so the board lists the whole plan, not only what has already been run.
    /// </summary>
    internal static IReadOnlyList<CollectionStatusRow> ComposeRows(
        IReadOnlyList<PlannedRow> planned,
        IReadOnlyDictionary<(string SampleType, string AnalysisName), RealFactRow> realFacts,
        IReadOnlyDictionary<string, int> collectedByType,
        IReadOnlyDictionary<string, Guid> responsibleBySampleType)
    {
        var rows = new List<CollectionStatusRow>(planned.Count);
        foreach (PlannedRow plan in planned)
        {
            realFacts.TryGetValue((plan.SampleType, plan.AnalysisName), out RealFactRow? fact);

            int collectedSamples = fact?.CollectedSamples
                ?? (collectedByType.TryGetValue(plan.SampleType, out int collected) ? collected : 0);

            responsibleBySampleType.TryGetValue(plan.SampleType, out Guid responsible);

            rows.Add(new CollectionStatusRow(
                plan.SampleType,
                plan.AnalysisName,
                responsible == Guid.Empty ? null : responsible,
                collectedSamples,
                fact?.PendingAnalyses ?? 0,
                fact?.CompletedAnalyses ?? 0));
        }

        return rows;
    }

    /// <summary>The case-insensitive comparer for the (sample type, analysis name) fact key.</summary>
    internal static IEqualityComparer<(string SampleType, string AnalysisName)> FactKeyComparer
        => StringPairComparer.Instance;

    /// <summary>
    /// Maps each sample type to the member responsible for it, by matching a cadastered collection role whose name
    /// equals the sample type to the plan's role assignment. Best-effort: a type without a same-named assigned role has
    /// no responsible.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, Guid>> ResolveResponsiblesBySampleTypeAsync(
        Guid planId,
        IDbConnection connection,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AssignmentRow> assignments = (await connection.QueryAsync<AssignmentRow>(
                new CommandDefinition(
                    "SELECT s.role_id AS roleid, s.user_id AS userid FROM experiments.collection_role_assignments AS s WHERE s.plan_id = @PlanId;",
                    new { PlanId = planId },
                    cancellationToken: cancellationToken)))
            .AsList();

        if (assignments.Count == 0)
            return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<CollectionRoleDto> roles = await _labConfiguration.GetCollectionRolesAsync(cancellationToken);

        var userByRole = assignments.ToDictionary(a => a.RoleId, a => a.UserId);

        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (CollectionRoleDto role in roles)
        {
            if (userByRole.TryGetValue(role.Id, out Guid userId))
                result[role.Name] = userId;
        }

        return result;
    }

    internal sealed record PlannedRow(string SampleType, string AnalysisName);

    internal sealed record RealFactRow(
        string SampleType,
        string AnalysisName,
        int CollectedSamples,
        int PendingAnalyses,
        int CompletedAnalyses);

    private sealed record CollectedByTypeRow(string SampleType, int CollectedSamples);

    private sealed record AssignmentRow(Guid RoleId, Guid UserId);

    /// <summary>Case-insensitive equality for the (sample type, analysis name) fact key.</summary>
    private sealed class StringPairComparer : IEqualityComparer<(string SampleType, string AnalysisName)>
    {
        public static readonly StringPairComparer Instance = new();

        public bool Equals((string SampleType, string AnalysisName) x, (string SampleType, string AnalysisName) y)
            => string.Equals(x.SampleType, y.SampleType, StringComparison.OrdinalIgnoreCase)
               && string.Equals(x.AnalysisName, y.AnalysisName, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string SampleType, string AnalysisName) obj)
            => HashCode.Combine(
                obj.SampleType.ToLowerInvariant(),
                obj.AnalysisName.ToLowerInvariant());
    }
}
