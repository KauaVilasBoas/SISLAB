using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Application.Units;

/// <summary>
/// Read-side query (card [E12] #76) that lists the active company's units for the configuration screen,
/// ordered by symbol. It reads <c>configuration.units</c> via Dapper — never the write DbContext. The
/// catalogue is small, so the list is returned whole (no pagination).
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from
/// the request, and the SELECT keeps <c>WHERE company_id = @CompanyId</c> (defense-in-depth, section 7).
/// </remarks>
public sealed record ListUnitsQuery : IQuery<IReadOnlyList<UnitListItem>>;

/// <summary>Flat read row for the unit configuration list.</summary>
public sealed record UnitListItem(Guid Id, string Symbol, string Name);

internal sealed class ListUnitsQueryHandler
    : BaseDataAccess, IQueryHandler<ListUnitsQuery, IReadOnlyList<UnitListItem>>
{
    private const string Sql =
        """
        SELECT
            u.id,
            u.symbol,
            u.name
        FROM configuration.units AS u
        WHERE u.company_id = @CompanyId
        ORDER BY u.symbol ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public ListUnitsQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<UnitListItem>> HandleAsync(
        ListUnitsQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        return (await connection.QueryAsync<UnitListItem>(
            new CommandDefinition(
                Sql,
                new { _tenantContext.CompanyId },
                cancellationToken: cancellationToken))).AsList();
    }
}
