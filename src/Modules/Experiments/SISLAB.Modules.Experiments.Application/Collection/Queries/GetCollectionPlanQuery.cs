using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Collection.Queries;

/// <summary>
/// Read-side query (SISLAB-08) that returns a batch's collection plan: the matrix (each sample type → planned analyses +
/// storage) and the roster (each role → assigned member). It reads <c>experiments.collection_plans</c> and its child
/// tables via Dapper — never the write DbContext — and shapes the flat contract the collection sheet screen needs.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from the request,
/// and every SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global query filter, so the
/// tenant guard is explicit (defense-in-depth, section 7).
/// </remarks>
public sealed record GetCollectionPlanQuery(Guid BatchId) : IQuery<CollectionPlanView>;

/// <summary>The plan header, its matrix rows and its role roster.</summary>
public sealed record CollectionPlanView(
    Guid Id,
    Guid ProjectId,
    Guid BatchId,
    IReadOnlyList<SampleRoutingView> Routings,
    IReadOnlyList<CollectionRoleAssignmentView> Assignments);

/// <summary>One matrix row: a sample type routed to its planned analyses and storage.</summary>
public sealed record SampleRoutingView(
    string SampleType,
    IReadOnlyList<string> PlannedAnalyses,
    Guid? StorageRoomId,
    string? StorageLabel,
    decimal? ConservationTempMinCelsius,
    decimal? ConservationTempMaxCelsius);

/// <summary>One roster row: a role assigned to a member (both by value).</summary>
public sealed record CollectionRoleAssignmentView(Guid RoleId, Guid UserId);

internal sealed class GetCollectionPlanQueryHandler
    : BaseDataAccess, IQueryHandler<GetCollectionPlanQuery, CollectionPlanView>
{
    internal const string PlanSql =
        """
        SELECT p.id, p.project_id AS projectid, p.batch_id AS batchid
        FROM experiments.collection_plans AS p
        WHERE p.company_id = @CompanyId AND p.batch_id = @BatchId;
        """;

    private const string RoutingsSql =
        """
        SELECT
            r.id,
            r.sample_type          AS sampletype,
            r.storage_room_id      AS storageroomid,
            r.storage_label        AS storagelabel,
            r.conservation_temp_min AS conservationtempmincelsius,
            r.conservation_temp_max AS conservationtempmaxcelsius
        FROM experiments.collection_sample_routings AS r
        WHERE r.plan_id = @PlanId
        ORDER BY r.sample_type ASC;
        """;

    private const string PlannedAnalysesSql =
        """
        SELECT a.routing_id AS routingid, a.name
        FROM experiments.collection_planned_analyses AS a
        INNER JOIN experiments.collection_sample_routings AS r ON r.id = a.routing_id
        WHERE r.plan_id = @PlanId
        ORDER BY a.name ASC;
        """;

    private const string AssignmentsSql =
        """
        SELECT s.role_id AS roleid, s.user_id AS userid
        FROM experiments.collection_role_assignments AS s
        WHERE s.plan_id = @PlanId;
        """;

    private readonly ITenantContext _tenantContext;

    public GetCollectionPlanQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<CollectionPlanView> HandleAsync(
        GetCollectionPlanQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        PlanRow? plan = await connection.QuerySingleOrDefaultAsync<PlanRow>(
            new CommandDefinition(
                PlanSql,
                new { _tenantContext.CompanyId, request.BatchId },
                cancellationToken: cancellationToken));

        if (plan is null)
            throw new NotFoundException($"No collection plan exists for batch '{request.BatchId}'.");

        IReadOnlyList<RoutingRow> routingRows = (await connection.QueryAsync<RoutingRow>(
            new CommandDefinition(RoutingsSql, new { PlanId = plan.Id }, cancellationToken: cancellationToken)))
            .AsList();

        ILookup<Guid, string> plannedByRouting = (await connection.QueryAsync<PlannedAnalysisRow>(
                new CommandDefinition(PlannedAnalysesSql, new { PlanId = plan.Id }, cancellationToken: cancellationToken)))
            .ToLookup(row => row.RoutingId, row => row.Name);

        IReadOnlyList<CollectionRoleAssignmentView> assignments = (await connection.QueryAsync<AssignmentRow>(
                new CommandDefinition(AssignmentsSql, new { PlanId = plan.Id }, cancellationToken: cancellationToken)))
            .Select(row => new CollectionRoleAssignmentView(row.RoleId, row.UserId))
            .ToList();

        IReadOnlyList<SampleRoutingView> routings = routingRows
            .Select(row => new SampleRoutingView(
                row.SampleType,
                plannedByRouting[row.Id].ToList(),
                row.StorageRoomId,
                row.StorageLabel,
                row.ConservationTempMinCelsius,
                row.ConservationTempMaxCelsius))
            .ToList();

        return new CollectionPlanView(plan.Id, plan.ProjectId, plan.BatchId, routings, assignments);
    }

    private sealed record PlanRow(Guid Id, Guid ProjectId, Guid BatchId);

    private sealed record RoutingRow(
        Guid Id,
        string SampleType,
        Guid? StorageRoomId,
        string? StorageLabel,
        decimal? ConservationTempMinCelsius,
        decimal? ConservationTempMaxCelsius);

    private sealed record PlannedAnalysisRow(Guid RoutingId, string Name);

    private sealed record AssignmentRow(Guid RoleId, Guid UserId);
}
