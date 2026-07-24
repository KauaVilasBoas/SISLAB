using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Projects.Queries;

/// <summary>
/// Read-side query (SISLAB-02) that lists a batch's animals with their inclusion decision — included/excluded and the
/// value that motivated it — so the operator can review the selection. Reads
/// <c>experiments.project_animals</c> (joined back to the tenant-checked project) via Dapper, never the write
/// DbContext. Optionally narrowed to a single inclusion status.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never the request,
/// and the SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global query filter, so the
/// tenant guard is explicit (defense-in-depth, section 7).
/// </remarks>
public sealed record ListAnimalSelectionQuery(Guid ProjectId, Guid BatchId)
    : IQuery<IReadOnlyList<AnimalSelectionListItem>>
{
    /// <summary>Optional inclusion-status filter ("Included"/"Excluded"); null lists every animal.</summary>
    public string? Status { get; init; }
}

/// <summary>Flat read row for the selection listing — an animal and (when applied) its inclusion decision.</summary>
public sealed record AnimalSelectionListItem(
    Guid Id,
    string Identifier,
    string Sex,
    string GroupName,
    string? InclusionStatus,
    string? InclusionParameterCode,
    decimal? InclusionDecidingValue,
    string? InclusionReason);

internal sealed class ListAnimalSelectionQueryHandler
    : BaseDataAccess, IQueryHandler<ListAnimalSelectionQuery, IReadOnlyList<AnimalSelectionListItem>>
{
    // Animals of one batch with their inclusion decision (null columns while no criterion was applied). company_id
    // keeps the mandatory tenant scoping; the optional status filter collapses to "all" when null.
    private const string Sql =
        """
        SELECT
            a.id,
            a.identifier,
            a.sex,
            g.name                     AS groupname,
            a.inclusion_status         AS inclusionstatus,
            a.inclusion_parameter_code AS inclusionparametercode,
            a.inclusion_deciding_value AS inclusiondecidingvalue,
            a.inclusion_reason         AS inclusionreason
        FROM experiments.project_animals AS a
        INNER JOIN experiments.project_groups AS g ON g.id = a.group_id
        INNER JOIN experiments.project_batches AS b ON b.id = g.batch_id
        INNER JOIN experiments.projects AS p ON p.id = b.project_id
        WHERE p.company_id = @CompanyId
          AND b.project_id = @ProjectId
          AND g.batch_id = @BatchId
          AND (@Status IS NULL OR a.inclusion_status = @Status)
        ORDER BY a.identifier ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public ListAnimalSelectionQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<AnimalSelectionListItem>> HandleAsync(
        ListAnimalSelectionQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        AnimalSelectionQueryParameters parameters = BuildParameters(request);

        return (await connection.QueryAsync<AnimalSelectionListItem>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();
    }

    /// <summary>
    /// Materializes the Dapper parameter set. The company id always comes from <see cref="ITenantContext"/> (never
    /// the request) and a blank status filter collapses to null. Extracted so the tenant guard and filter
    /// normalization are unit-testable without a live database.
    /// </summary>
    internal AnimalSelectionQueryParameters BuildParameters(ListAnimalSelectionQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        ProjectId: request.ProjectId,
        BatchId: request.BatchId,
        Status: string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim());
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="ListAnimalSelectionQuery"/>. Property names match the
/// <c>@Parameter</c> tokens exactly (Dapper binds by name). Exposed to the module's tests so the tenant guard and
/// filter normalization can be asserted without a live database.
/// </summary>
internal sealed record AnimalSelectionQueryParameters(
    Guid CompanyId,
    Guid ProjectId,
    Guid BatchId,
    string? Status);
