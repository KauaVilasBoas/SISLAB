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
    Guid? ResponsibleUserId,
    IReadOnlyList<ExperimentStepDetail> Steps,
    IReadOnlyList<PlateWellDetail> Wells,
    ExperimentCalculationDetail? Calculation);

/// <summary>A step in the experiment's flow, as shown on the detail page.</summary>
public sealed record ExperimentStepDetail(
    Guid Id,
    int Order,
    string Kind,
    string Title,
    string? PerformedBy,
    DateTime? PerformedAtUtc,
    string? Notes,
    IReadOnlyList<Guid> ResponsibleUserIds);

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
            e.responsible_user_id  AS responsibleuserid,
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
            s.id,
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

    // Step responsibles (card [E11]) for this experiment, scoped through the tenant-checked experiment. One row
    // per (step, user); the handler groups them by step_id onto each step.
    private const string StepResponsiblesSql =
        """
        SELECT
            r.step_id  AS stepid,
            r.user_id  AS userid
        FROM experiments.experiment_step_responsibles AS r
        INNER JOIN experiments.experiment_steps AS s
            ON s.id = r.step_id
        INNER JOIN experiments.experiments AS e
            ON e.id = s.experiment_id
        WHERE e.company_id = @CompanyId
          AND s.experiment_id = @ExperimentId;
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

        IReadOnlyList<ExperimentStepRow> stepRows = (await connection.QueryAsync<ExperimentStepRow>(
            new CommandDefinition(StepsSql, parameters, cancellationToken: cancellationToken))).AsList();

        IReadOnlyList<StepResponsibleRow> responsibleRows = (await connection.QueryAsync<StepResponsibleRow>(
            new CommandDefinition(StepResponsiblesSql, parameters, cancellationToken: cancellationToken))).AsList();

        ILookup<Guid, Guid> responsiblesByStep =
            responsibleRows.ToLookup(row => row.StepId, row => row.UserId);

        IReadOnlyList<ExperimentStepDetail> steps = stepRows
            .Select(step => new ExperimentStepDetail(
                step.Id,
                step.Order,
                step.Kind,
                step.Title,
                step.PerformedBy,
                step.PerformedAtUtc,
                step.Notes,
                responsiblesByStep[step.Id].ToList()))
            .ToList();

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
            header.ResponsibleUserId,
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
        Guid? ResponsibleUserId,
        string? FormulaName,
        string? FormulaExpression,
        DateTime? AppliedAtUtc,
        string? ResultJson);

    private sealed record ExperimentStepRow(
        Guid Id,
        int Order,
        string Kind,
        string Title,
        string? PerformedBy,
        DateTime? PerformedAtUtc,
        string? Notes);

    private sealed record StepResponsibleRow(Guid StepId, Guid UserId);
}
