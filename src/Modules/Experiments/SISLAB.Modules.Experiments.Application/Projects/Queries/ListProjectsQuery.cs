using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Projects.Queries;

/// <summary>
/// Read-side query (card [E11] #73) that lists the active company's in vivo projects for the projects page,
/// most recent first, optionally narrowed by <see cref="Status"/>, paginated. Reads
/// <c>experiments.projects</c> (with derived batch/animal counts) via Dapper — never the write DbContext — and
/// projects the flat <see cref="ProjectListItem"/> the table needs.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never the
/// request, and the SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global query
/// filter, so the tenant guard is explicit (defense-in-depth, section 7).
/// </remarks>
public sealed record ListProjectsQuery : PagedQuery<PagedResult<ProjectListItem>>
{
    /// <summary>Optional lifecycle-status filter (matched against the persisted name); null lists every status.</summary>
    public string? Status { get; init; }
}

/// <summary>Flat read row for the projects list. Never leaks the aggregate or its value objects.</summary>
public sealed record ProjectListItem(
    Guid Id,
    string Name,
    string Species,
    string Status,
    int DesignVersion,
    int BatchCount,
    int AnimalCount);

internal sealed class ListProjectsQueryHandler
    : BaseDataAccess, IQueryHandler<ListProjectsQuery, PagedResult<ProjectListItem>>
{
    // Active company's projects, newest first (id DESC as a stable order — projects carry no created_at column).
    // Batch/animal counts are derived by correlated subqueries against the owned tables. company_id keeps the
    // mandatory tenant scoping.
    private const string Sql =
        """
        WITH records AS (
            SELECT
                p.id,
                p.name,
                p.species,
                p.status,
                p.current_design_version,
                (SELECT COUNT(*) FROM experiments.project_batches b WHERE b.project_id = p.id)::int AS batch_count,
                (SELECT COUNT(*)
                 FROM experiments.project_animals a
                 JOIN experiments.project_groups g ON g.id = a.group_id
                 JOIN experiments.project_batches b ON b.id = g.batch_id
                 WHERE b.project_id = p.id)::int AS animal_count,
                ROW_NUMBER() OVER (ORDER BY p.id DESC) AS row_number,
                (COUNT(*) OVER ())::int AS total_rows
            FROM experiments.projects AS p
            WHERE p.company_id = @CompanyId
              AND (@Status IS NULL OR p.status = @Status)
        )
        SELECT
            id,
            name,
            species,
            status,
            current_design_version AS designversion,
            batch_count            AS batchcount,
            animal_count           AS animalcount,
            total_rows             AS totalrows
        FROM records
        WHERE row_number BETWEEN @FirstResult AND @LastResult
        ORDER BY row_number;
        """;

    private readonly ITenantContext _tenantContext;

    public ListProjectsQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<PagedResult<ProjectListItem>> HandleAsync(
        ListProjectsQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        ProjectsListQueryParameters parameters = BuildParameters(request);

        IReadOnlyList<ProjectListRow> rows = (await connection.QueryAsync<ProjectListRow>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        int totalCount = rows.Count > 0 ? rows[0].TotalRows : 0;

        IReadOnlyList<ProjectListItem> items = rows.Select(row => row.ToListItem()).ToList();

        return new PagedResult<ProjectListItem>(items, totalCount, request.Page, request.PageSize);
    }

    /// <summary>
    /// Materializes the Dapper parameter set. The company id always comes from <see cref="ITenantContext"/>
    /// (never the request), and a blank filter collapses to null. Extracted so the tenant guard, filter
    /// normalization and pagination are unit-testable without a live database.
    /// </summary>
    internal ProjectsListQueryParameters BuildParameters(ListProjectsQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        Status: string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim(),
        FirstResult: request.FirstResult,
        LastResult: request.LastResult);

    private sealed record ProjectListRow(
        Guid Id,
        string Name,
        string Species,
        string Status,
        int DesignVersion,
        int BatchCount,
        int AnimalCount,
        int TotalRows)
    {
        public ProjectListItem ToListItem() =>
            new(Id, Name, Species, Status, DesignVersion, BatchCount, AnimalCount);
    }
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="ListProjectsQuery"/>. Property names match the
/// <c>@Parameter</c> tokens exactly (Dapper binds by name). Exposed to the module's tests so the tenant guard,
/// filter normalization and pagination can be asserted without a live database.
/// </summary>
internal sealed record ProjectsListQueryParameters(
    Guid CompanyId,
    string? Status,
    int FirstResult,
    int LastResult);
