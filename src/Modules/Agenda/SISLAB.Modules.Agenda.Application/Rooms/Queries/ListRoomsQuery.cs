using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Agenda.Application.Rooms.Queries;

/// <summary>Returns all active rooms for the tenant (used to populate room-picker dropdowns).</summary>
public sealed record ListRoomsQuery : IQuery<IReadOnlyList<RoomListItem>>;

public sealed record RoomListItem(
    Guid Id,
    string Name,
    int Capacity,
    string Type);

internal sealed class ListRoomsQueryHandler
    : BaseDataAccess, IQueryHandler<ListRoomsQuery, IReadOnlyList<RoomListItem>>
{
    private const string Sql =
        """
        SELECT id, name, capacity, type
        FROM agenda.rooms
        WHERE company_id = @CompanyId AND is_active = TRUE
        ORDER BY name;
        """;

    private readonly ITenantContext _tenantContext;

    public ListRoomsQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory) => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<RoomListItem>> HandleAsync(
        ListRoomsQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();
        return (await connection.QueryAsync<RoomListItem>(
            new CommandDefinition(Sql,
                new { CompanyId = _tenantContext.CompanyId },
                cancellationToken: cancellationToken))).AsList();
    }
}
