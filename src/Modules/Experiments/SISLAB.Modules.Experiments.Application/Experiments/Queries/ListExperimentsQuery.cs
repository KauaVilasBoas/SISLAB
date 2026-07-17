using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Experiments.Queries;

/// <summary>
/// Read-side query (decision card #68) that lists the active company's experiments for the experiments page,
/// most recent first, optionally narrowed by <see cref="Status"/> and/or <see cref="Type"/>, paginated. Reads
/// <c>experiments.experiments</c> via Dapper — never the write DbContext — and projects the flat
/// <see cref="ExperimentListItem"/> the table needs.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never the
/// request, and the SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global query
/// filter, so the tenant guard is explicit (defense-in-depth, section 7).
/// </remarks>
public sealed record ListExperimentsQuery : PagedQuery<PagedResult<ExperimentListItem>>
{
    /// <summary>Optional lifecycle-status filter (matched against the persisted name); null lists every status.</summary>
    public string? Status { get; init; }

    /// <summary>Optional type filter (matched against the persisted discriminator); null lists every type.</summary>
    public string? Type { get; init; }
}

/// <summary>Flat read row for the experiments list. Never leaks the aggregate or its value objects.</summary>
public sealed record ExperimentListItem(
    Guid Id,
    string Title,
    string Type,
    string Status,
    bool IsCalculated,
    DateTime CreatedAtUtc,
    string CreatedBy);

internal sealed class ListExperimentsQueryHandler
    : BaseDataAccess, IQueryHandler<ListExperimentsQuery, PagedResult<ExperimentListItem>>
{
    // Active company's experiments, newest first (created_at_utc DESC, id DESC as a stable tie-breaker). The
    // optional filters match the persisted status/type names. is_calculated is derived from the presence of the
    // frozen formula snapshot. company_id keeps the mandatory tenant scoping.
    private const string Sql =
        """
        WITH records AS (
            SELECT
                e.id,
                e.title,
                e.type,
                e.status,
                (e.formula_result_json IS NOT NULL) AS is_calculated,
                e.created_at_utc,
                e.created_by,
                ROW_NUMBER() OVER (ORDER BY e.created_at_utc DESC, e.id DESC) AS row_number,
                (COUNT(*) OVER ())::int AS total_rows
            FROM experiments.experiments AS e
            WHERE e.company_id = @CompanyId
              AND (@Status IS NULL OR e.status = @Status)
              AND (@Type IS NULL OR e.type = @Type)
        )
        SELECT
            id,
            title,
            type,
            status,
            is_calculated   AS iscalculated,
            created_at_utc  AS createdatutc,
            created_by      AS createdby,
            total_rows      AS totalrows
        FROM records
        WHERE row_number BETWEEN @FirstResult AND @LastResult
        ORDER BY row_number;
        """;

    private readonly ITenantContext _tenantContext;

    public ListExperimentsQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<PagedResult<ExperimentListItem>> HandleAsync(
        ListExperimentsQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        ExperimentsListQueryParameters parameters = BuildParameters(request);

        IReadOnlyList<ExperimentListRow> rows = (await connection.QueryAsync<ExperimentListRow>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        int totalCount = rows.Count > 0 ? rows[0].TotalRows : 0;

        IReadOnlyList<ExperimentListItem> items = rows.Select(row => row.ToListItem()).ToList();

        return new PagedResult<ExperimentListItem>(items, totalCount, request.Page, request.PageSize);
    }

    /// <summary>
    /// Materializes the Dapper parameter set. The company id always comes from <see cref="ITenantContext"/>
    /// (never the request), and blank filters collapse to null. Extracted so the tenant guard, filter
    /// normalization and pagination are unit-testable without a live database.
    /// </summary>
    internal ExperimentsListQueryParameters BuildParameters(ListExperimentsQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        Status: NormalizeFilter(request.Status),
        Type: NormalizeFilter(request.Type),
        FirstResult: request.FirstResult,
        LastResult: request.LastResult);

    private static string? NormalizeFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ExperimentListRow(
        Guid Id,
        string Title,
        string Type,
        string Status,
        bool IsCalculated,
        DateTime CreatedAtUtc,
        string CreatedBy,
        int TotalRows)
    {
        public ExperimentListItem ToListItem() =>
            new(Id, Title, Type, Status, IsCalculated, CreatedAtUtc, CreatedBy);
    }
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="ListExperimentsQuery"/>. Property names match the
/// <c>@Parameter</c> tokens exactly (Dapper binds by name). Exposed to the module's tests so the tenant guard,
/// filter normalization and pagination can be asserted without a live database.
/// </summary>
internal sealed record ExperimentsListQueryParameters(
    Guid CompanyId,
    string? Status,
    string? Type,
    int FirstResult,
    int LastResult);
