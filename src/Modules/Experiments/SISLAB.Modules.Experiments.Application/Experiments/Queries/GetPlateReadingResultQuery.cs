using System.Data;
using System.Text.Json;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Experiments.Queries;

/// <summary>
/// Read-side query (decision card #68) that returns a viability experiment's plate as the grid the UI renders:
/// one entry per designed well with its role, imported absorbance and — when the experiment has been calculated
/// — its % viability read from the frozen snapshot. Reads <c>experiments.wells</c> and the experiment's snapshot
/// via Dapper, never the write DbContext.
/// </summary>
/// <remarks>
/// The viability percentages are taken from the immutable snapshot JSON produced by <c>viability@v1</c>, never
/// recomputed here — the read-side reflects exactly what was frozen at calculation time (reproducibility). Every
/// SELECT is tenant-scoped with <c>WHERE company_id = @CompanyId</c>.
/// </remarks>
public sealed record GetPlateReadingResultQuery(Guid ExperimentId) : IQuery<PlateReadingResult>;

/// <summary>The plate grid result: the experiment id and one <see cref="PlateWellResult"/> per designed well.</summary>
public sealed record PlateReadingResult(
    Guid ExperimentId,
    bool IsCalculated,
    IReadOnlyList<PlateWellResult> Wells);

/// <summary>
/// A single well on the result grid: coordinate, role, absorbance and — once calculated — the assay's computed
/// value for that well (% viability for the viability assay, NO µM for the nitric-oxide assay). Kept unit-agnostic
/// here; the UI labels the unit from the experiment type.
/// </summary>
public sealed record PlateWellResult(
    char Row,
    int Column,
    string Role,
    decimal? RawAbsorbance,
    decimal? ComputedValue,
    bool IsExcluded,
    string? ExclusionReason,
    string? ExcludedBy);

internal sealed class GetPlateReadingResultQueryHandler
    : BaseDataAccess, IQueryHandler<GetPlateReadingResultQuery, PlateReadingResult>
{
    private const string ExperimentSql =
        """
        SELECT
            e.id,
            e.formula_result_json AS resultjson
        FROM experiments.experiments AS e
        WHERE e.company_id = @CompanyId
          AND e.id = @ExperimentId;
        """;

    private const string WellsSql =
        """
        SELECT
            w.well_row         AS row,
            w.well_column      AS column,
            w.role,
            w.raw_absorbance   AS rawabsorbance,
            w.is_excluded      AS isexcluded,
            w.exclusion_reason AS exclusionreason,
            w.excluded_by      AS excludedby
        FROM experiments.wells AS w
        INNER JOIN experiments.experiments AS e
            ON e.id = w.experiment_id
        WHERE e.company_id = @CompanyId
          AND w.experiment_id = @ExperimentId
        ORDER BY w.well_row ASC, w.well_column ASC;
        """;

    private static readonly JsonSerializerOptions ResultDeserializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ITenantContext _tenantContext;

    public GetPlateReadingResultQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<PlateReadingResult> HandleAsync(
        GetPlateReadingResultQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        var parameters = new { CompanyId = _tenantContext.CompanyId, request.ExperimentId };

        ExperimentResultRow experiment = await connection.QuerySingleOrDefaultAsync<ExperimentResultRow>(
            new CommandDefinition(ExperimentSql, parameters, cancellationToken: cancellationToken))
            ?? throw new NotFoundException($"Experiment '{request.ExperimentId}' was not found.");

        IReadOnlyList<PlateWellRow> wellRows = (await connection.QueryAsync<PlateWellRow>(
            new CommandDefinition(WellsSql, parameters, cancellationToken: cancellationToken))).AsList();

        IReadOnlyDictionary<string, decimal> computedByWell = ParseComputedValues(experiment.ResultJson);

        IReadOnlyList<PlateWellResult> wells = wellRows
            .Select(row => new PlateWellResult(
                row.Row,
                row.Column,
                row.Role,
                row.RawAbsorbance,
                computedByWell.TryGetValue($"{row.Row}{row.Column}", out decimal value) ? value : null,
                row.IsExcluded,
                row.ExclusionReason,
                row.ExcludedBy))
            .ToList();

        return new PlateReadingResult(experiment.Id, experiment.ResultJson is not null, wells);
    }

    /// <summary>
    /// Extracts the per-well computed value from the frozen snapshot JSON, unit-agnostically: the viability
    /// payload carries <c>viabilityPct</c> and the nitric-oxide payload carries <c>concentrationUm</c>, both under
    /// a <c>wells[].well</c> key. The read-side reflects exactly what the strategy stored; a null/malformed snapshot
    /// simply yields no values (the grid still shows the raw readings).
    /// </summary>
    private static IReadOnlyDictionary<string, decimal> ParseComputedValues(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            return new Dictionary<string, decimal>();

        try
        {
            SnapshotPayload? payload =
                JsonSerializer.Deserialize<SnapshotPayload>(resultJson, ResultDeserializerOptions);

            if (payload?.Wells is null)
                return new Dictionary<string, decimal>();

            var values = new Dictionary<string, decimal>();
            foreach (SnapshotWell well in payload.Wells)
            {
                decimal? computed = well.ViabilityPct ?? well.ConcentrationUm;
                if (computed is { } value)
                    values[well.Well] = value;
            }

            return values;
        }
        catch (JsonException)
        {
            return new Dictionary<string, decimal>();
        }
    }

    private sealed record ExperimentResultRow(Guid Id, string? ResultJson);

    private sealed record PlateWellRow(
        char Row,
        int Column,
        string Role,
        decimal? RawAbsorbance,
        bool IsExcluded,
        string? ExclusionReason,
        string? ExcludedBy);

    /// <summary>Shape of the snapshot JSON common to both strategies (only the per-well computed value is read).</summary>
    private sealed record SnapshotPayload(IReadOnlyList<SnapshotWell>? Wells);

    private sealed record SnapshotWell(string Well, decimal? ViabilityPct, decimal? ConcentrationUm);
}
