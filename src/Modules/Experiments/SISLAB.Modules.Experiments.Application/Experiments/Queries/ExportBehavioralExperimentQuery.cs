using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Experiments.Application.Export;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Experiments.Queries;

/// <summary>
/// Read-side query (card [E11] #31) that renders a calculated in vivo behavioural experiment as a GraphPad
/// Prism-pasteable CSV laid out as <b>group × timepoint</b>. It reads the frozen snapshot from
/// <c>experiments.experiments</c> and the animal→dose-group mapping from the source experiment's batch on the
/// <c>Project</c> aggregate (both via Dapper), then delegates the CSV shaping to the
/// <see cref="IInVivoPrismFormatter"/> registered for the snapshot's formula code — never recomputing, so the
/// export reflects exactly what was signed off.
/// </summary>
/// <remarks>
/// Export is a pure read of the immutable snapshot, so it is a query (no state change) and the endpoint is
/// page-level <c>[Authorize]</c>, not permission-gated — any member may export. Every SELECT keeps the mandatory
/// <c>WHERE company_id = @CompanyId</c> tenant guard (the group mapping is scoped by joining back to the
/// tenant-checked project). An experiment that has not been calculated yet has no snapshot and yields a
/// <see cref="ConflictException"/>.
/// </remarks>
public sealed record ExportBehavioralExperimentQuery(Guid ExperimentId) : IQuery<ExperimentExportDto>;

internal sealed class ExportBehavioralExperimentQueryHandler
    : BaseDataAccess, IQueryHandler<ExportBehavioralExperimentQuery, ExperimentExportDto>
{
    private const string HeaderSql =
        """
        SELECT
            e.id,
            e.title,
            e.project_id          AS projectid,
            e.batch_id            AS batchid,
            e.formula_name        AS formulaname,
            e.formula_result_json AS resultjson
        FROM experiments.experiments AS e
        WHERE e.company_id = @CompanyId
          AND e.id = @ExperimentId;
        """;

    // Animal → dose-group mapping for the experiment's batch, scoped to the tenant via the project join. Held by
    // value; the formatter pivots the snapshot thresholds under these groups.
    private const string GroupsSql =
        """
        SELECT
            a.id       AS animalid,
            g.id       AS groupid,
            g.name     AS groupname,
            g.dose_amount AS doseamount,
            g.dose_unit   AS doseunit,
            a.identifier  AS animalidentifier
        FROM experiments.project_animals AS a
        INNER JOIN experiments.project_groups  AS g ON g.id = a.group_id
        INNER JOIN experiments.project_batches AS b ON b.id = g.batch_id
        INNER JOIN experiments.projects        AS p ON p.id = b.project_id
        WHERE p.company_id = @CompanyId
          AND b.project_id = @ProjectId
          AND b.id = @BatchId;
        """;

    private readonly ITenantContext _tenantContext;
    private readonly IInVivoPrismFormatterResolver _formatterResolver;

    public ExportBehavioralExperimentQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext,
        IInVivoPrismFormatterResolver formatterResolver)
        : base(connectionFactory)
    {
        _tenantContext = tenantContext;
        _formatterResolver = formatterResolver;
    }

    public async Task<ExperimentExportDto> HandleAsync(
        ExportBehavioralExperimentQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        Guid companyId = _tenantContext.CompanyId;

        ExportHeaderRow header = await connection.QuerySingleOrDefaultAsync<ExportHeaderRow>(
            new CommandDefinition(
                HeaderSql,
                new { CompanyId = companyId, request.ExperimentId },
                cancellationToken: cancellationToken))
            ?? throw new NotFoundException($"Experiment '{request.ExperimentId}' was not found.");

        if (header.FormulaName is null || header.ResultJson is null)
            throw new ConflictException(
                $"Experiment '{request.ExperimentId}' has not been calculated yet and cannot be exported.");

        IReadOnlyList<AnimalGroupAssignment> animalGroups = (await connection.QueryAsync<AnimalGroupAssignment>(
            new CommandDefinition(
                GroupsSql,
                new { CompanyId = companyId, ProjectId = header.ProjectId, BatchId = header.BatchId },
                cancellationToken: cancellationToken))).AsList();

        IInVivoPrismFormatter formatter = _formatterResolver.Resolve(header.FormulaName);
        string csv = formatter.Format(header.ResultJson, animalGroups);

        string fileName = $"experimento-invivo-{request.ExperimentId}.csv";

        return new ExperimentExportDto(fileName, "text/csv", csv);
    }

    private sealed record ExportHeaderRow(
        Guid Id,
        string Title,
        Guid ProjectId,
        Guid BatchId,
        string? FormulaName,
        string? ResultJson);
}
