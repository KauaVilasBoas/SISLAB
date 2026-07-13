using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Application.ReferenceRanges;

/// <summary>
/// Read-side query (card [E12] #76) that lists the active company's reference ranges for the configuration
/// screen, ordered by analyte then species. It reads <c>configuration.reference_ranges</c> via Dapper — never
/// the write DbContext.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from
/// the request, and the SELECT keeps <c>WHERE company_id = @CompanyId</c> (defense-in-depth, section 7).
/// </remarks>
public sealed record ListReferenceRangesQuery : IQuery<IReadOnlyList<ReferenceRangeListItem>>;

/// <summary>Flat read row for the reference-range configuration list.</summary>
public sealed record ReferenceRangeListItem(
    Guid Id,
    string Analyte,
    string Species,
    decimal? Minimum,
    decimal? Maximum,
    string? Unit);

internal sealed class ListReferenceRangesQueryHandler
    : BaseDataAccess, IQueryHandler<ListReferenceRangesQuery, IReadOnlyList<ReferenceRangeListItem>>
{
    private const string Sql =
        """
        SELECT
            r.id,
            r.analyte,
            r.species,
            r.minimum,
            r.maximum,
            r.unit
        FROM configuration.reference_ranges AS r
        WHERE r.company_id = @CompanyId
        ORDER BY r.analyte ASC, r.species ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public ListReferenceRangesQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<ReferenceRangeListItem>> HandleAsync(
        ListReferenceRangesQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        return (await connection.QueryAsync<ReferenceRangeListItem>(
            new CommandDefinition(
                Sql,
                new { _tenantContext.CompanyId },
                cancellationToken: cancellationToken))).AsList();
    }
}
