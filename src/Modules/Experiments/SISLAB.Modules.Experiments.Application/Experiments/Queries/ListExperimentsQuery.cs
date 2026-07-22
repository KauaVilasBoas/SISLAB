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
    /// <summary>
    /// Optional lifecycle-status <b>multi-select</b> filter (card [E11], DP-6): each value is matched against the
    /// persisted status name; an experiment matches when its status is in the set (OR). Null or empty lists every
    /// status.
    /// </summary>
    public IReadOnlyList<string>? Statuses { get; init; }

    /// <summary>Optional type filter (matched against the persisted discriminator); null lists every type.</summary>
    public string? Type { get; init; }

    /// <summary>
    /// Optional responsible <b>multi-select</b> filter (card [E11]): each value is a Lumen user id. An experiment
    /// matches when <b>any</b> of these users is its lead responsible <i>or</i> a responsible of <i>any</i> of its
    /// steps (OR within the filter). Null or empty applies no responsible filter. Deduplicated via EXISTS so an
    /// experiment with several matching steps is never counted twice.
    /// </summary>
    public IReadOnlyList<Guid>? ResponsibleUserIds { get; init; }
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
              AND (@HasStatusFilter = FALSE OR e.status = ANY(@Statuses))
              AND (@Type IS NULL OR e.type = @Type)
              AND (
                    @HasResponsibleFilter = FALSE
                    OR e.responsible_user_id = ANY(@ResponsibleUserIds)
                    -- OR a responsible of ANY of this experiment's steps is in the selected set. EXISTS (not a
                    -- JOIN) keeps one row per experiment, so the total_rows window count stays correct even when
                    -- several steps match.
                    OR EXISTS (
                        SELECT 1
                        FROM experiments.experiment_steps AS s
                        INNER JOIN experiments.experiment_step_responsibles AS r
                            ON r.step_id = s.id
                        WHERE s.experiment_id = e.id
                          AND r.user_id = ANY(@ResponsibleUserIds)
                    )
                  )
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
    internal ExperimentsListQueryParameters BuildParameters(ListExperimentsQuery request)
    {
        string[]? statuses = NormalizeList(request.Statuses);
        Guid[]? responsibleUserIds = NormalizeGuidList(request.ResponsibleUserIds);

        return new ExperimentsListQueryParameters(
            CompanyId: _tenantContext.CompanyId,
            HasStatusFilter: statuses is not null,
            Statuses: statuses,
            Type: NormalizeFilter(request.Type),
            HasResponsibleFilter: responsibleUserIds is not null,
            ResponsibleUserIds: responsibleUserIds,
            FirstResult: request.FirstResult,
            LastResult: request.LastResult);
    }

    private static string? NormalizeFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Trims/de-dupes the status values, dropping blanks; returns null when nothing usable remains.</summary>
    private static string[]? NormalizeList(IReadOnlyList<string>? values)
    {
        if (values is null)
            return null;

        string[] normalized = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return normalized.Length == 0 ? null : normalized;
    }

    /// <summary>De-dupes the user ids, dropping empties; returns null when nothing usable remains.</summary>
    private static Guid[]? NormalizeGuidList(IReadOnlyList<Guid>? values)
    {
        if (values is null)
            return null;

        Guid[] normalized = values
            .Where(value => value != Guid.Empty)
            .Distinct()
            .ToArray();

        return normalized.Length == 0 ? null : normalized;
    }

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
    bool HasStatusFilter,
    string[]? Statuses,
    string? Type,
    bool HasResponsibleFilter,
    Guid[]? ResponsibleUserIds,
    int FirstResult,
    int LastResult);
