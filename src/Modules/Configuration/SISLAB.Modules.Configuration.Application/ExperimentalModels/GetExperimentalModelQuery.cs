using System.Data;
using System.Text.Json;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Configuration.Domain.ExperimentalModels;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Application.ExperimentalModels;

/// <summary>
/// Read-side query (SISLAB-04) that resolves one experimental model of the active company by id, with its full
/// protocol/timepoints/parameters/groups/dilution payload. It reads <c>configuration.experimental_models</c> via
/// Dapper and returns <see langword="null"/> when no such model exists for the tenant.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant scoping.</b> The company comes from <see cref="ITenantContext"/> by the handler, never from the
/// request, and the SELECT keeps <c>WHERE company_id = @CompanyId</c> — so a caller can never resolve a model from
/// another tenant through this surface.
/// </para>
/// <para>
/// <b>jsonb columns.</b> The timepoints/parameters/groups are stored as <c>jsonb</c>; Dapper returns them as raw
/// JSON text, which the handler deserializes into the view's structured shape (the same DTO the write-side value
/// converter uses), so the read model mirrors what was persisted without touching the write DbContext.
/// </para>
/// </remarks>
public sealed record GetExperimentalModelQuery(Guid ModelId) : IQuery<ExperimentalModelView?>;

/// <summary>Structured read view of one experimental model.</summary>
public sealed record ExperimentalModelView(
    Guid Id,
    string Name,
    string? Description,
    InductionProtocolView Induction,
    IReadOnlyList<string> Timepoints,
    IReadOnlyList<string> Parameters,
    IReadOnlyList<StandardGroupView> Groups,
    DilutionDefaultsView DilutionDefaults);

/// <summary>Read view of the induction protocol.</summary>
public sealed record InductionProtocolView(int Administrations, int IntervalDays, int ReferenceDayAfterInduction);

/// <summary>Read view of one standard group.</summary>
public sealed record StandardGroupView(string Name, StandardGroupKind Kind, decimal? DoseAmount, string? DoseUnit);

/// <summary>Read view of the default dilution parameters.</summary>
public sealed record DilutionDefaultsView(decimal MicrolitresPerGram, string DefaultDiluent);

internal sealed class GetExperimentalModelQueryHandler
    : BaseDataAccess, IQueryHandler<GetExperimentalModelQuery, ExperimentalModelView?>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    private const string Sql =
        """
        SELECT
            m.id,
            m.name,
            m.description,
            m.induction_administrations AS administrations,
            m.induction_interval_days AS intervaldays,
            m.induction_reference_day AS referencedayafterinduction,
            m.timepoints,
            m.parameters,
            m.groups,
            m.ratio_microlitres_per_gram AS microlitrespergram,
            m.default_diluent AS defaultdiluent
        FROM configuration.experimental_models AS m
        WHERE m.company_id = @CompanyId
          AND m.id = @ModelId
        LIMIT 1;
        """;

    private readonly ITenantContext _tenantContext;

    public GetExperimentalModelQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<ExperimentalModelView?> HandleAsync(
        GetExperimentalModelQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        ExperimentalModelRow? row = await connection.QuerySingleOrDefaultAsync<ExperimentalModelRow>(
            new CommandDefinition(
                Sql,
                new { _tenantContext.CompanyId, request.ModelId },
                cancellationToken: cancellationToken));

        if (row is null)
            return null;

        return new ExperimentalModelView(
            row.Id,
            row.Name,
            row.Description,
            new InductionProtocolView(row.Administrations, row.IntervalDays, row.ReferenceDayAfterInduction),
            DeserializeList<string>(row.Timepoints),
            DeserializeList<string>(row.Parameters),
            DeserializeList<StandardGroupView>(row.Groups),
            new DilutionDefaultsView(row.MicrolitresPerGram, row.DefaultDiluent));
    }

    private static List<T> DeserializeList<T>(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];

    /// <summary>Flat Dapper row, with the jsonb columns as raw JSON text to be deserialized by the handler.</summary>
    private sealed record ExperimentalModelRow(
        Guid Id,
        string Name,
        string? Description,
        int Administrations,
        int IntervalDays,
        int ReferenceDayAfterInduction,
        string Timepoints,
        string Parameters,
        string Groups,
        decimal MicrolitresPerGram,
        string DefaultDiluent);
}
