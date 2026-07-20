using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Experiments.Contracts;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.PublicApi;

/// <summary>
/// Adapter implementing the Experiments module's public boundary <see cref="IExperimentDirectory"/>
/// (card [E10.4] #4). It resolves experiment titles by id for the active company through a single tenant-scoped
/// Dapper lookup — the same read-side technology the module already uses — and never exposes the internal
/// <c>Experiment</c> aggregate across the boundary.
/// </summary>
/// <remarks>
/// The mandatory <c>WHERE company_id = @CompanyId</c> keeps the lookup tenant-scoped (the EF global filter does
/// not cover Dapper, section 5). An empty id set short-circuits to an empty map with no round-trip.
/// </remarks>
internal sealed class ExperimentDirectory : BaseDataAccess, IExperimentDirectory
{
    private const string Sql =
        """
        SELECT e.id, e.title
        FROM experiments.experiments AS e
        WHERE e.company_id = @CompanyId
          AND e.id = ANY(@ExperimentIds);
        """;

    private readonly ITenantContext _tenantContext;

    public ExperimentDirectory(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory) => _tenantContext = tenantContext;

    public async Task<IReadOnlyDictionary<Guid, string>> GetTitlesAsync(
        IReadOnlyCollection<Guid> experimentIds,
        CancellationToken ct)
    {
        Guid[] distinctIds = experimentIds.Distinct().ToArray();
        if (distinctIds.Length == 0)
            return new Dictionary<Guid, string>();

        using IDbConnection connection = await OpenConnectionAsync();
        IEnumerable<(Guid Id, string Title)> rows = await connection.QueryAsync<(Guid, string)>(
            new CommandDefinition(
                Sql,
                new { CompanyId = _tenantContext.CompanyId, ExperimentIds = distinctIds },
                cancellationToken: ct));

        return rows.ToDictionary(row => row.Id, row => row.Title);
    }
}
