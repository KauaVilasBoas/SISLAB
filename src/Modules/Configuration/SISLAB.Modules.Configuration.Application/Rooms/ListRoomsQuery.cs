using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Application.Rooms;

/// <summary>
/// Read-side query (card [E12] #76) that lists the active company's rooms for the configuration screen,
/// ordered by name. It reads <c>configuration.rooms</c> via Dapper — never the write DbContext.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from
/// the request, and the SELECT keeps <c>WHERE company_id = @CompanyId</c> (defense-in-depth, section 7).
/// </remarks>
public sealed record ListRoomsQuery : IQuery<IReadOnlyList<RoomListItem>>;

/// <summary>Flat read row for the room configuration list.</summary>
public sealed record RoomListItem(Guid Id, string Name, bool RequiresAuthorization);

internal sealed class ListRoomsQueryHandler
    : BaseDataAccess, IQueryHandler<ListRoomsQuery, IReadOnlyList<RoomListItem>>
{
    private const string Sql =
        """
        SELECT
            r.id,
            r.name,
            r.requires_authorization AS requiresauthorization
        FROM configuration.rooms AS r
        WHERE r.company_id = @CompanyId
        ORDER BY r.name ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public ListRoomsQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<RoomListItem>> HandleAsync(
        ListRoomsQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        return (await connection.QueryAsync<RoomListItem>(
            new CommandDefinition(
                Sql,
                new { _tenantContext.CompanyId },
                cancellationToken: cancellationToken))).AsList();
    }
}
