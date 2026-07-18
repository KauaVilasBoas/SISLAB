using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Biobank.Queries;

/// <summary>
/// Read-side query (card [E11] #89) that lists the active company's biobank samples for the biobank page, newest
/// first, optionally narrowed by <see cref="ProjectId"/> or <see cref="SampleType"/>, paginated. Reads
/// <c>experiments.samples</c> (with the <b>derived</b> remaining balance computed from the analyses) via Dapper —
/// never the write DbContext — and projects the flat <see cref="SampleListItem"/> the table needs.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never the
/// request, and the SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global query
/// filter, so the tenant guard is explicit (defense-in-depth, section 7). The remaining balance mirrors the
/// aggregate's derived property: <c>collected − SUM(consumed)</c>, so the read never trusts a stored field.
/// </remarks>
public sealed record ListSamplesQuery : PagedQuery<PagedResult<SampleListItem>>
{
    /// <summary>Optional project filter (by value); null lists every project's samples.</summary>
    public Guid? ProjectId { get; init; }

    /// <summary>Optional sample-type filter (matched against the persisted name); null lists every type.</summary>
    public string? Type { get; init; }
}

/// <summary>Flat read row for the samples list. Never leaks the aggregate or its value objects.</summary>
public sealed record SampleListItem(
    Guid Id,
    string Code,
    string Type,
    Guid AnimalId,
    Guid SourceExperimentId,
    decimal CollectedQuantity,
    decimal ConsumedQuantity,
    decimal RemainingQuantity,
    string Unit,
    int AnalysisCount,
    DateTime CollectedAtUtc);

internal sealed class ListSamplesQueryHandler
    : BaseDataAccess, IQueryHandler<ListSamplesQuery, PagedResult<SampleListItem>>
{
    // Active company's samples, newest first. The consumed/remaining amounts are derived from the owned analyses
    // (COALESCE handles a sample with no analyses yet). company_id keeps the mandatory tenant scoping.
    private const string Sql =
        """
        WITH consumption AS (
            SELECT a.sample_id, COALESCE(SUM(a.consumed_value), 0) AS consumed_value
            FROM experiments.sample_analyses a
            GROUP BY a.sample_id
        ),
        records AS (
            SELECT
                s.id,
                s.code,
                s.type,
                s.animal_id,
                s.source_experiment_id,
                s.collected_value,
                COALESCE(c.consumed_value, 0)                        AS consumed_value,
                s.collected_value - COALESCE(c.consumed_value, 0)    AS remaining_value,
                s.collected_unit,
                (SELECT COUNT(*) FROM experiments.sample_analyses a WHERE a.sample_id = s.id) AS analysis_count,
                s.collected_at_utc,
                ROW_NUMBER() OVER (ORDER BY s.collected_at_utc DESC, s.id DESC) AS row_number,
                (COUNT(*) OVER ())::int AS total_rows
            FROM experiments.samples AS s
            LEFT JOIN consumption AS c ON c.sample_id = s.id
            WHERE s.company_id = @CompanyId
              AND (@ProjectId IS NULL OR s.project_id = @ProjectId)
              AND (@Type IS NULL OR s.type = @Type)
        )
        SELECT
            id,
            code,
            type,
            animal_id            AS animalid,
            source_experiment_id AS sourceexperimentid,
            collected_value      AS collectedquantity,
            consumed_value       AS consumedquantity,
            remaining_value      AS remainingquantity,
            collected_unit       AS unit,
            analysis_count       AS analysiscount,
            collected_at_utc     AS collectedatutc,
            total_rows           AS totalrows
        FROM records
        WHERE row_number BETWEEN @FirstResult AND @LastResult
        ORDER BY row_number;
        """;

    private readonly ITenantContext _tenantContext;

    public ListSamplesQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<PagedResult<SampleListItem>> HandleAsync(
        ListSamplesQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        SamplesListQueryParameters parameters = BuildParameters(request);

        IReadOnlyList<SampleListRow> rows = (await connection.QueryAsync<SampleListRow>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        int totalCount = rows.Count > 0 ? rows[0].TotalRows : 0;

        IReadOnlyList<SampleListItem> items = rows.Select(row => row.ToListItem()).ToList();

        return new PagedResult<SampleListItem>(items, totalCount, request.Page, request.PageSize);
    }

    /// <summary>
    /// Materializes the Dapper parameter set. The company id always comes from <see cref="ITenantContext"/>
    /// (never the request), and a blank filter collapses to null. Extracted so the tenant guard, filter
    /// normalization and pagination are unit-testable without a live database.
    /// </summary>
    internal SamplesListQueryParameters BuildParameters(ListSamplesQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        ProjectId: request.ProjectId,
        Type: string.IsNullOrWhiteSpace(request.Type) ? null : request.Type.Trim(),
        FirstResult: request.FirstResult,
        LastResult: request.LastResult);

    private sealed record SampleListRow(
        Guid Id,
        string Code,
        string Type,
        Guid AnimalId,
        Guid SourceExperimentId,
        decimal CollectedQuantity,
        decimal ConsumedQuantity,
        decimal RemainingQuantity,
        string Unit,
        int AnalysisCount,
        DateTime CollectedAtUtc,
        int TotalRows)
    {
        public SampleListItem ToListItem() => new(
            Id, Code, Type, AnimalId, SourceExperimentId,
            CollectedQuantity, ConsumedQuantity, RemainingQuantity, Unit, AnalysisCount, CollectedAtUtc);
    }
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="ListSamplesQuery"/>. Property names match the <c>@Parameter</c>
/// tokens exactly (Dapper binds by name). Exposed to the module's tests so the tenant guard, filter normalization
/// and pagination can be asserted without a live database.
/// </summary>
internal sealed record SamplesListQueryParameters(
    Guid CompanyId,
    Guid? ProjectId,
    string? Type,
    int FirstResult,
    int LastResult);
