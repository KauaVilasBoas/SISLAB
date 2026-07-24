using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Projects.Queries;

/// <summary>
/// Read-side query (SISLAB-01) that lists a project's confirmed in vivo solution preparations for the active
/// company — the frozen dose × weight × relation snapshots per batch/group — optionally narrowed to a single batch
/// and/or group. Reads <c>experiments.project_solution_preparations</c> via Dapper (joined back to the tenant-checked
/// project), never the write DbContext.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never the request,
/// and the SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global query filter, so the
/// tenant guard is explicit (defense-in-depth, section 7).
/// </remarks>
public sealed record ListSolutionPreparationsQuery(Guid ProjectId) : IQuery<IReadOnlyList<SolutionPreparationListItem>>
{
    /// <summary>Optional batch filter; null lists preparations across every batch of the project.</summary>
    public Guid? BatchId { get; init; }

    /// <summary>Optional group filter; null lists preparations across every group.</summary>
    public Guid? GroupId { get; init; }
}

/// <summary>
/// Flat read row for the solution-preparations list (SISLAB-01). Never leaks the aggregate — the frozen input and
/// result are projected column-by-column so the UI can render exactly what the operator confirmed.
/// </summary>
public sealed record SolutionPreparationListItem(
    Guid Id,
    Guid BatchId,
    Guid GroupId,
    string GroupName,
    bool IsVehicleOnly,
    decimal DoseAmountGramsPerKilogram,
    decimal GroupWeightGrams,
    decimal RelationWeightGrams,
    decimal RelationMicrolitresPerGram,
    string CompoundState,
    decimal? DensityGramsPerMillilitre,
    decimal CompoundMassGrams,
    decimal? CompoundVolumeMicrolitres,
    decimal FinalVolumeMicrolitres,
    decimal DiluentVolumeMicrolitres,
    string FormulaCode,
    string PreparedBy,
    DateTime PreparedAtUtc);

internal sealed class ListSolutionPreparationsQueryHandler
    : BaseDataAccess, IQueryHandler<ListSolutionPreparationsQuery, IReadOnlyList<SolutionPreparationListItem>>
{
    // Preparations of one project, resolved to the dose group's operator-facing name. company_id keeps the mandatory
    // tenant scoping; the optional batch/group filters collapse to "all" when null.
    private const string Sql =
        """
        SELECT
            sp.id,
            sp.batch_id                       AS batchid,
            sp.group_id                       AS groupid,
            g.name                            AS groupname,
            sp.is_vehicle_only                AS isvehicleonly,
            sp.dose_amount_g_per_kg           AS doseamountgramsperkilogram,
            sp.group_weight_grams             AS groupweightgrams,
            sp.relation_weight_grams          AS relationweightgrams,
            sp.relation_microlitres_per_gram  AS relationmicrolitrespergram,
            sp.compound_state                 AS compoundstate,
            sp.density_g_per_ml               AS densitygramspermillilitre,
            sp.compound_mass_grams            AS compoundmassgrams,
            sp.compound_volume_microlitres    AS compoundvolumemicrolitres,
            sp.final_volume_microlitres       AS finalvolumemicrolitres,
            sp.diluent_volume_microlitres     AS diluentvolumemicrolitres,
            sp.formula_code                   AS formulacode,
            sp.prepared_by                    AS preparedby,
            sp.prepared_at_utc                AS preparedatutc
        FROM experiments.project_solution_preparations AS sp
        INNER JOIN experiments.projects AS p ON p.id = sp.project_id
        INNER JOIN experiments.project_groups AS g ON g.id = sp.group_id
        WHERE p.company_id = @CompanyId
          AND sp.project_id = @ProjectId
          AND (@BatchId IS NULL OR sp.batch_id = @BatchId)
          AND (@GroupId IS NULL OR sp.group_id = @GroupId)
        ORDER BY sp.prepared_at_utc DESC;
        """;

    private readonly ITenantContext _tenantContext;

    public ListSolutionPreparationsQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<SolutionPreparationListItem>> HandleAsync(
        ListSolutionPreparationsQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        SolutionPreparationsQueryParameters parameters = BuildParameters(request);

        return (await connection.QueryAsync<SolutionPreparationListItem>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();
    }

    /// <summary>
    /// Materializes the Dapper parameter set. The company id always comes from <see cref="ITenantContext"/> (never
    /// the request). Extracted so the tenant guard is unit-testable without a live database.
    /// </summary>
    internal SolutionPreparationsQueryParameters BuildParameters(ListSolutionPreparationsQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        ProjectId: request.ProjectId,
        BatchId: request.BatchId,
        GroupId: request.GroupId);
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="ListSolutionPreparationsQuery"/>. Property names match the
/// <c>@Parameter</c> tokens exactly (Dapper binds by name). Exposed to the module's tests so the tenant guard can be
/// asserted without a live database.
/// </summary>
internal sealed record SolutionPreparationsQueryParameters(
    Guid CompanyId,
    Guid ProjectId,
    Guid? BatchId,
    Guid? GroupId);
