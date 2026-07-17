using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Experiments.Queries;

/// <summary>
/// Read-side query (decision card #68) that returns a single experiment's detail for the active company: its
/// header, its ordered step flow, its designed plate wells and — once calculated — the frozen result snapshot.
/// Reads <c>experiments.*</c> via Dapper in one round-trip (three result sets), never the write DbContext.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> Every SELECT keeps <c>WHERE company_id = @CompanyId</c> (steps/wells are scoped by
/// joining back to the tenant-checked experiment), so the read-side tenant guard is explicit. An experiment
/// that does not exist for the active company yields a <see cref="NotFoundException"/>.
/// </remarks>
public sealed record GetExperimentQuery(Guid ExperimentId) : IQuery<ExperimentDetail>;

/// <summary>Full detail of an experiment: header + steps + wells + optional calculation snapshot.</summary>
public sealed record ExperimentDetail(
    Guid Id,
    string Title,
    string? Description,
    string Type,
    string Status,
    Guid? CompoundPartnerId,
    DateTime CreatedAtUtc,
    string CreatedBy,
    IReadOnlyList<ExperimentStepDetail> Steps,
    IReadOnlyList<PlateWellDetail> Wells,
    ExperimentCalculationDetail? Calculation);

/// <summary>A step in the experiment's flow, as shown on the detail page.</summary>
public sealed record ExperimentStepDetail(
    int Order,
    string Kind,
    string Title,
    string? PerformedBy,
    DateTime? PerformedAtUtc,
    string? Notes);

/// <summary>A designed well on the detail page, with its optional reading.</summary>
public sealed record PlateWellDetail(
    char Row,
    int Column,
    string Role,
    decimal? ConcentrationUm,
    string? SampleId,
    decimal? RawAbsorbance);

/// <summary>The frozen calculation snapshot, when the experiment has been calculated.</summary>
public sealed record ExperimentCalculationDetail(
    string FormulaName,
    string FormulaExpression,
    DateTime AppliedAtUtc,
    string ResultJson);

internal sealed class GetExperimentQueryHandler
    : BaseDataAccess, IQueryHandler<GetExperimentQuery, ExperimentDetail>
{
    private const string HeaderSql =
        """
        SELECT
            e.id,
            e.title,
            e.description,
            e.type,
            e.status,
            e.compound_partner_id  AS compoundpartnerid,
            e.created_at_utc       AS createdatutc,
            e.created_by           AS createdby,
            e.formula_name         AS formulaname,
            e.formula_expression   AS formulaexpression,
            e.formula_applied_at_utc AS appliedatutc,
            e.formula_result_json  AS resultjson
        FROM experiments.experiments AS e
        WHERE e.company_id = @CompanyId
          AND e.id = @ExperimentId;
        """;

    private const string StepsSql =
        """
        SELECT
            s.step_order         AS order,
            s.kind,
            s.title,
            s.performed_by       AS performedby,
            s.performed_at_utc   AS performedatutc,
            s.notes
        FROM experiments.experiment_steps AS s
        INNER JOIN experiments.experiments AS e
            ON e.id = s.experiment_id
        WHERE e.company_id = @CompanyId
          AND s.experiment_id = @ExperimentId
        ORDER BY s.step_order ASC;
        """;

    private const string WellsSql =
        """
        SELECT
            w.well_row        AS row,
            w.well_column     AS column,
            w.role,
            w.concentration_um AS concentrationum,
            w.sample_id       AS sampleid,
            w.raw_absorbance  AS rawabsorbance
        FROM experiments.wells AS w
        INNER JOIN experiments.experiments AS e
            ON e.id = w.experiment_id
        WHERE e.company_id = @CompanyId
          AND w.experiment_id = @ExperimentId
        ORDER BY w.well_row ASC, w.well_column ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public GetExperimentQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<ExperimentDetail> HandleAsync(
        GetExperimentQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        var parameters = new { CompanyId = _tenantContext.CompanyId, request.ExperimentId };

        ExperimentHeaderRow header = await connection.QuerySingleOrDefaultAsync<ExperimentHeaderRow>(
            new CommandDefinition(HeaderSql, parameters, cancellationToken: cancellationToken))
            ?? throw new NotFoundException($"Experiment '{request.ExperimentId}' was not found.");

        IReadOnlyList<ExperimentStepDetail> steps = (await connection.QueryAsync<ExperimentStepDetail>(
            new CommandDefinition(StepsSql, parameters, cancellationToken: cancellationToken))).AsList();

        IReadOnlyList<PlateWellDetail> wells = (await connection.QueryAsync<PlateWellDetail>(
            new CommandDefinition(WellsSql, parameters, cancellationToken: cancellationToken))).AsList();

        ExperimentCalculationDetail? calculation = header.ResultJson is null
            ? null
            : new ExperimentCalculationDetail(
                header.FormulaName!,
                header.FormulaExpression!,
                header.AppliedAtUtc!.Value,
                header.ResultJson);

        return new ExperimentDetail(
            header.Id,
            header.Title,
            header.Description,
            header.Type,
            header.Status,
            header.CompoundPartnerId,
            header.CreatedAtUtc,
            header.CreatedBy,
            steps,
            wells,
            calculation);
    }

    private sealed record ExperimentHeaderRow(
        Guid Id,
        string Title,
        string? Description,
        string Type,
        string Status,
        Guid? CompoundPartnerId,
        DateTime CreatedAtUtc,
        string CreatedBy,
        string? FormulaName,
        string? FormulaExpression,
        DateTime? AppliedAtUtc,
        string? ResultJson);
}
