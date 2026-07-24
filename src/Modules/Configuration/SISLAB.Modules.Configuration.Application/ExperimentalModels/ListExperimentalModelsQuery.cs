using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Application.ExperimentalModels;

/// <summary>
/// Read-side query (SISLAB-04) that lists the active company's experimental models for the configuration screen,
/// ordered by name. It reads <c>configuration.experimental_models</c> via Dapper — never the write DbContext — and
/// returns a compact summary row (the full protocol/groups payload is served by the detail query).
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from the
/// request, and the SELECT keeps <c>WHERE company_id = @CompanyId</c> (defense-in-depth, section 7).
/// </remarks>
public sealed record ListExperimentalModelsQuery : IQuery<IReadOnlyList<ExperimentalModelListItem>>;

/// <summary>Flat read row summarizing one experimental model for the listing.</summary>
public sealed record ExperimentalModelListItem(
    Guid Id,
    string Name,
    string? Description,
    int InductionAdministrations,
    int ReferenceDayAfterInduction);

internal sealed class ListExperimentalModelsQueryHandler
    : BaseDataAccess, IQueryHandler<ListExperimentalModelsQuery, IReadOnlyList<ExperimentalModelListItem>>
{
    private const string Sql =
        """
        SELECT
            m.id,
            m.name,
            m.description,
            m.induction_administrations AS inductionadministrations,
            m.induction_reference_day AS referencedayafterinduction
        FROM configuration.experimental_models AS m
        WHERE m.company_id = @CompanyId
        ORDER BY m.name ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public ListExperimentalModelsQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<ExperimentalModelListItem>> HandleAsync(
        ListExperimentalModelsQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        return (await connection.QueryAsync<ExperimentalModelListItem>(
            new CommandDefinition(
                Sql,
                new { _tenantContext.CompanyId },
                cancellationToken: cancellationToken))).AsList();
    }
}
