using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Configuration.Domain.InclusionCriteria;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Application.InclusionCriteria;

/// <summary>
/// Read-side query (SISLAB-02) that lists the active company's inclusion criteria, ordered by parameter. It reads
/// <c>configuration.inclusion_criteria</c> via Dapper — never the write DbContext — and is the source both for the
/// configuration screen and for the <c>ILabConfiguration</c> port the Experiments module consumes to apply the
/// selection.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from the
/// request, and the SELECT keeps <c>WHERE company_id = @CompanyId</c> (defense-in-depth, section 7).
/// </remarks>
public sealed record ListInclusionCriteriaQuery : IQuery<IReadOnlyList<InclusionCriterionListItem>>;

/// <summary>Flat read row for one inclusion criterion. The operator is the persisted enum name.</summary>
public sealed record InclusionCriterionListItem(
    Guid Id,
    string ParameterCode,
    ComparisonOperator Operator,
    decimal Threshold,
    string Unit);

internal sealed class ListInclusionCriteriaQueryHandler
    : BaseDataAccess, IQueryHandler<ListInclusionCriteriaQuery, IReadOnlyList<InclusionCriterionListItem>>
{
    private const string Sql =
        """
        SELECT
            c.id,
            c.parameter_code AS parametercode,
            c.operator,
            c.threshold,
            c.unit
        FROM configuration.inclusion_criteria AS c
        WHERE c.company_id = @CompanyId
        ORDER BY c.parameter_code ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public ListInclusionCriteriaQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<InclusionCriterionListItem>> HandleAsync(
        ListInclusionCriteriaQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        return (await connection.QueryAsync<InclusionCriterionListItem>(
            new CommandDefinition(
                Sql,
                new { _tenantContext.CompanyId },
                cancellationToken: cancellationToken))).AsList();
    }
}
