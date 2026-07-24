using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Projects.Queries;

/// <summary>
/// Read-side query (SISLAB-02) that lists a project's physiological readings for the active company — glicemia/peso
/// per animal per timepoint — optionally narrowed by parameter code and/or a single animal. Reads
/// <c>experiments.project_physiological_readings</c> via Dapper (joined back to the tenant-checked project), never
/// the write DbContext.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never the request,
/// and the SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global query filter, so the
/// tenant guard is explicit (defense-in-depth, section 7).
/// </remarks>
public sealed record ListPhysiologicalReadingsQuery(Guid ProjectId) : IQuery<IReadOnlyList<PhysiologicalReadingListItem>>
{
    /// <summary>Optional parameter-code filter (e.g. "glicemia"); null lists every parameter.</summary>
    public string? ParameterCode { get; init; }

    /// <summary>Optional single-animal filter; null lists every animal in the project.</summary>
    public Guid? AnimalId { get; init; }
}

/// <summary>Flat read row for the physiological readings list. Never leaks the aggregate.</summary>
public sealed record PhysiologicalReadingListItem(
    Guid Id,
    Guid AnimalId,
    string AnimalIdentifier,
    string ParameterCode,
    decimal Value,
    string Unit,
    string TimepointLabel,
    string RecordedBy,
    DateTime RecordedAtUtc);

internal sealed class ListPhysiologicalReadingsQueryHandler
    : BaseDataAccess, IQueryHandler<ListPhysiologicalReadingsQuery, IReadOnlyList<PhysiologicalReadingListItem>>
{
    // Readings of one project, resolved to the animal's operator-facing identifier. company_id keeps the mandatory
    // tenant scoping; the optional parameter/animal filters collapse to "all" when null.
    private const string Sql =
        """
        SELECT
            r.id,
            r.animal_id       AS animalid,
            a.identifier      AS animalidentifier,
            r.parameter_code  AS parametercode,
            r.value,
            r.unit,
            r.timepoint_label AS timepointlabel,
            r.recorded_by     AS recordedby,
            r.recorded_at_utc AS recordedatutc
        FROM experiments.project_physiological_readings AS r
        INNER JOIN experiments.projects AS p ON p.id = r.project_id
        INNER JOIN experiments.project_animals AS a ON a.id = r.animal_id
        WHERE p.company_id = @CompanyId
          AND r.project_id = @ProjectId
          AND (@ParameterCode IS NULL OR lower(r.parameter_code) = lower(@ParameterCode))
          AND (@AnimalId IS NULL OR r.animal_id = @AnimalId)
        ORDER BY a.identifier ASC, r.recorded_at_utc ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public ListPhysiologicalReadingsQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<PhysiologicalReadingListItem>> HandleAsync(
        ListPhysiologicalReadingsQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        PhysiologicalReadingsQueryParameters parameters = BuildParameters(request);

        return (await connection.QueryAsync<PhysiologicalReadingListItem>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();
    }

    /// <summary>
    /// Materializes the Dapper parameter set. The company id always comes from <see cref="ITenantContext"/> (never
    /// the request) and a blank parameter-code filter collapses to null. Extracted so the tenant guard and filter
    /// normalization are unit-testable without a live database.
    /// </summary>
    internal PhysiologicalReadingsQueryParameters BuildParameters(ListPhysiologicalReadingsQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        ProjectId: request.ProjectId,
        ParameterCode: string.IsNullOrWhiteSpace(request.ParameterCode) ? null : request.ParameterCode.Trim(),
        AnimalId: request.AnimalId);
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="ListPhysiologicalReadingsQuery"/>. Property names match the
/// <c>@Parameter</c> tokens exactly (Dapper binds by name). Exposed to the module's tests so the tenant guard and
/// filter normalization can be asserted without a live database.
/// </summary>
internal sealed record PhysiologicalReadingsQueryParameters(
    Guid CompanyId,
    Guid ProjectId,
    string? ParameterCode,
    Guid? AnimalId);
