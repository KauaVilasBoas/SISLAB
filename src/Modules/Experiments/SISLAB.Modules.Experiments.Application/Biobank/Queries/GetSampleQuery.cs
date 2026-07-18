using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Biobank.Queries;

/// <summary>
/// Read-side query (card [E11] #89) that returns a single biobank sample's detail for the active company: its
/// header (with origin ids by value and conservation range), its derived remaining balance, and the list of
/// analyses run against it. Reads <c>experiments.*</c> via Dapper in two result sets, never the write DbContext.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> Every SELECT keeps <c>WHERE company_id = @CompanyId</c> (the analyses are scoped by
/// joining back to the tenant-checked sample). A sample that does not exist for the active company yields a
/// <see cref="NotFoundException"/>.
/// </remarks>
public sealed record GetSampleQuery(Guid SampleId) : IQuery<SampleDetail>;

/// <summary>Full detail of a sample: header + derived balance + analyses.</summary>
public sealed record SampleDetail(
    Guid Id,
    string Code,
    string Type,
    Guid ProjectId,
    Guid BatchId,
    Guid AnimalId,
    Guid SourceExperimentId,
    decimal CollectedQuantity,
    decimal ConsumedQuantity,
    decimal RemainingQuantity,
    string Unit,
    decimal? ConservationTempMinCelsius,
    decimal? ConservationTempMaxCelsius,
    string? StorageLabel,
    string? Notes,
    string CollectedBy,
    DateTime CollectedAtUtc,
    IReadOnlyList<SampleAnalysisDetail> Analyses);

/// <summary>An analysis run against the sample, as shown on the detail page.</summary>
public sealed record SampleAnalysisDetail(
    Guid Id,
    string Name,
    decimal ConsumedQuantity,
    string Unit,
    string Status,
    string? Result,
    string PerformedBy,
    DateTime PerformedAtUtc);

internal sealed class GetSampleQueryHandler
    : BaseDataAccess, IQueryHandler<GetSampleQuery, SampleDetail>
{
    private const string HeaderSql =
        """
        SELECT
            s.id,
            s.code,
            s.type,
            s.project_id            AS projectid,
            s.batch_id              AS batchid,
            s.animal_id             AS animalid,
            s.source_experiment_id  AS sourceexperimentid,
            s.collected_value       AS collectedquantity,
            COALESCE((SELECT SUM(a.consumed_value)
                      FROM experiments.sample_analyses a
                      WHERE a.sample_id = s.id), 0)             AS consumedquantity,
            s.collected_value - COALESCE((SELECT SUM(a.consumed_value)
                      FROM experiments.sample_analyses a
                      WHERE a.sample_id = s.id), 0)             AS remainingquantity,
            s.collected_unit        AS unit,
            s.conservation_temp_min AS conservationtempmincelsius,
            s.conservation_temp_max AS conservationtempmaxcelsius,
            s.storage_label         AS storagelabel,
            s.notes,
            s.collected_by          AS collectedby,
            s.collected_at_utc      AS collectedatutc
        FROM experiments.samples AS s
        WHERE s.company_id = @CompanyId
          AND s.id = @SampleId;
        """;

    private const string AnalysesSql =
        """
        SELECT
            a.id,
            a.name,
            a.consumed_value    AS consumedquantity,
            a.consumed_unit     AS unit,
            a.status,
            a.result,
            a.performed_by      AS performedby,
            a.performed_at_utc  AS performedatutc
        FROM experiments.sample_analyses AS a
        INNER JOIN experiments.samples AS s ON s.id = a.sample_id
        WHERE s.company_id = @CompanyId
          AND a.sample_id = @SampleId
        ORDER BY a.performed_at_utc ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public GetSampleQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<SampleDetail> HandleAsync(GetSampleQuery request, CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        var parameters = new { CompanyId = _tenantContext.CompanyId, request.SampleId };

        SampleHeaderRow header = await connection.QuerySingleOrDefaultAsync<SampleHeaderRow>(
            new CommandDefinition(HeaderSql, parameters, cancellationToken: cancellationToken))
            ?? throw new NotFoundException($"Sample '{request.SampleId}' was not found.");

        IReadOnlyList<SampleAnalysisDetail> analyses = (await connection.QueryAsync<SampleAnalysisDetail>(
            new CommandDefinition(AnalysesSql, parameters, cancellationToken: cancellationToken))).AsList();

        return new SampleDetail(
            header.Id,
            header.Code,
            header.Type,
            header.ProjectId,
            header.BatchId,
            header.AnimalId,
            header.SourceExperimentId,
            header.CollectedQuantity,
            header.ConsumedQuantity,
            header.RemainingQuantity,
            header.Unit,
            header.ConservationTempMinCelsius,
            header.ConservationTempMaxCelsius,
            header.StorageLabel,
            header.Notes,
            header.CollectedBy,
            header.CollectedAtUtc,
            analyses);
    }

    private sealed record SampleHeaderRow(
        Guid Id,
        string Code,
        string Type,
        Guid ProjectId,
        Guid BatchId,
        Guid AnimalId,
        Guid SourceExperimentId,
        decimal CollectedQuantity,
        decimal ConsumedQuantity,
        decimal RemainingQuantity,
        string Unit,
        decimal? ConservationTempMinCelsius,
        decimal? ConservationTempMaxCelsius,
        string? StorageLabel,
        string? Notes,
        string CollectedBy,
        DateTime CollectedAtUtc);
}
