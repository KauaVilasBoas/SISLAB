using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Projects.Queries;

/// <summary>
/// Read-side query (SISLAB-03) that summarizes a batch's physiological readings <b>by cage</b> for one parameter — the
/// basal/glicemia view the researcher looks at <i>before</i> randomization ("dados por caixa, depois divide os
/// grupos"). Returns, per cage, the animal count that had a reading and the mean/min/max value, ready for the Prism
/// pre-randomization column. Reads <c>experiments.*</c> via Dapper (joined back to the tenant-checked project), never
/// the write DbContext.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never the request, and
/// the SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global query filter, so the tenant
/// guard is explicit (defense-in-depth, section 7). The latest reading per animal for the requested parameter/timepoint
/// is what the cage aggregate is built from, so a re-measured animal counts once.
/// </remarks>
public sealed record ListBaselineByCageQuery(Guid ProjectId, Guid BatchId, string ParameterCode)
    : IQuery<IReadOnlyList<CageBaselineItem>>
{
    /// <summary>Optional timepoint filter (e.g. "basal"); null aggregates the latest reading of the parameter.</summary>
    public string? TimepointLabel { get; init; }
}

/// <summary>Per-cage baseline summary of one parameter for the pre-randomization view.</summary>
public sealed record CageBaselineItem(
    Guid CageId,
    string CageName,
    int AnimalsWithReading,
    decimal? MeanValue,
    decimal? MinValue,
    decimal? MaxValue,
    string? Unit);

internal sealed class ListBaselineByCageQueryHandler
    : BaseDataAccess, IQueryHandler<ListBaselineByCageQuery, IReadOnlyList<CageBaselineItem>>
{
    // For each animal, take its latest reading of the parameter (optionally at a timepoint), then aggregate by cage.
    // company_id keeps the mandatory tenant scoping; every cage of the batch is returned (LEFT JOIN) so an unmeasured
    // cage still appears with a zero count.
    private const string Sql =
        """
        WITH latest AS (
            SELECT DISTINCT ON (r.animal_id)
                r.animal_id,
                r.value,
                r.unit
            FROM experiments.project_physiological_readings AS r
            INNER JOIN experiments.projects AS p ON p.id = r.project_id
            WHERE p.company_id = @CompanyId
              AND r.project_id = @ProjectId
              AND lower(r.parameter_code) = lower(@ParameterCode)
              AND (@TimepointLabel IS NULL OR lower(r.timepoint_label) = lower(@TimepointLabel))
            ORDER BY r.animal_id, r.recorded_at_utc DESC
        )
        SELECT
            c.id                       AS cageid,
            c.name                     AS cagename,
            count(l.animal_id)         AS animalswithreading,
            avg(l.value)               AS meanvalue,
            min(l.value)               AS minvalue,
            max(l.value)               AS maxvalue,
            max(l.unit)                AS unit
        FROM experiments.project_cages AS c
        INNER JOIN experiments.project_batches AS b ON b.id = c.batch_id
        INNER JOIN experiments.projects AS p ON p.id = b.project_id
        LEFT JOIN experiments.project_animals AS a ON a.cage_id = c.id
        LEFT JOIN latest AS l ON l.animal_id = a.id
        WHERE p.company_id = @CompanyId
          AND b.project_id = @ProjectId
          AND c.batch_id = @BatchId
        GROUP BY c.id, c.name
        ORDER BY c.name ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public ListBaselineByCageQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<CageBaselineItem>> HandleAsync(
        ListBaselineByCageQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        BaselineByCageQueryParameters parameters = BuildParameters(request);

        return (await connection.QueryAsync<CageBaselineItem>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();
    }

    /// <summary>
    /// Materializes the Dapper parameter set. The company id always comes from <see cref="ITenantContext"/> (never the
    /// request); a blank timepoint filter collapses to null. Extracted so the tenant guard and filter normalization are
    /// unit-testable without a live database.
    /// </summary>
    internal BaselineByCageQueryParameters BuildParameters(ListBaselineByCageQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        ProjectId: request.ProjectId,
        BatchId: request.BatchId,
        ParameterCode: request.ParameterCode.Trim(),
        TimepointLabel: string.IsNullOrWhiteSpace(request.TimepointLabel) ? null : request.TimepointLabel.Trim());
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="ListBaselineByCageQuery"/>. Property names match the
/// <c>@Parameter</c> tokens exactly (Dapper binds by name). Exposed to the module's tests so the tenant guard and
/// filter normalization can be asserted without a live database.
/// </summary>
internal sealed record BaselineByCageQueryParameters(
    Guid CompanyId,
    Guid ProjectId,
    Guid BatchId,
    string ParameterCode,
    string? TimepointLabel);
