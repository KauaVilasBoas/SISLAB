using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Experiments.Application.Export;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Experiments.Queries;

/// <summary>
/// Read-side query (card [E11] #79) that renders a calculated experiment as a GraphPad Prism-pasteable CSV. It
/// reads the frozen snapshot from <c>experiments.experiments</c> via Dapper and delegates the CSV shaping to the
/// <see cref="IPrismCsvFormatter"/> registered for the snapshot's formula code — never recomputing, so the export
/// reflects exactly what was signed off.
/// </summary>
/// <remarks>
/// Export is a pure read of the immutable snapshot, so it is modelled as a query (no state change) and the
/// endpoint is page-level <c>[Authorize]</c>, not permission-gated — any member may export. The SELECT keeps the
/// mandatory <c>WHERE company_id = @CompanyId</c> tenant guard. An experiment that has not been calculated yet has
/// no snapshot to export and yields a <see cref="ConflictException"/>.
/// </remarks>
public sealed record ExportExperimentQuery(Guid ExperimentId) : IQuery<ExperimentExportDto>;

/// <summary>The rendered export: the download file name, its content type and the CSV body.</summary>
public sealed record ExperimentExportDto(string FileName, string ContentType, string CsvContent);

internal sealed class ExportExperimentQueryHandler
    : BaseDataAccess, IQueryHandler<ExportExperimentQuery, ExperimentExportDto>
{
    private const string Sql =
        """
        SELECT
            e.id,
            e.title,
            e.formula_name        AS formulaname,
            e.formula_result_json AS resultjson
        FROM experiments.experiments AS e
        WHERE e.company_id = @CompanyId
          AND e.id = @ExperimentId;
        """;

    private readonly ITenantContext _tenantContext;
    private readonly IPrismCsvFormatterResolver _formatterResolver;

    public ExportExperimentQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext,
        IPrismCsvFormatterResolver formatterResolver)
        : base(connectionFactory)
    {
        _tenantContext = tenantContext;
        _formatterResolver = formatterResolver;
    }

    public async Task<ExperimentExportDto> HandleAsync(
        ExportExperimentQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        var parameters = new { CompanyId = _tenantContext.CompanyId, request.ExperimentId };

        ExportRow row = await connection.QuerySingleOrDefaultAsync<ExportRow>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))
            ?? throw new NotFoundException($"Experiment '{request.ExperimentId}' was not found.");

        if (row.FormulaName is null || row.ResultJson is null)
            throw new ConflictException(
                $"Experiment '{request.ExperimentId}' has not been calculated yet and cannot be exported.");

        IPrismCsvFormatter formatter = _formatterResolver.Resolve(row.FormulaName);
        string csv = formatter.Format(row.ResultJson);

        string fileName = $"experimento-{request.ExperimentId}.csv";

        return new ExperimentExportDto(fileName, "text/csv", csv);
    }

    private sealed record ExportRow(Guid Id, string Title, string? FormulaName, string? ResultJson);
}
