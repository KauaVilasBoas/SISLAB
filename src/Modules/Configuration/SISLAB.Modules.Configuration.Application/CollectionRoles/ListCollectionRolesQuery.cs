using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Application.CollectionRoles;

/// <summary>
/// Read-side query (SISLAB-08) that lists the active company's collection roles, ordered by name. It reads
/// <c>configuration.collection_roles</c> via Dapper — never the write DbContext — and is the source both for the
/// configuration screen and for the <c>ILabConfiguration</c> port the Experiments module consumes to validate a role
/// assignment by value.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from the
/// request, and the SELECT keeps <c>WHERE company_id = @CompanyId</c> (defense-in-depth, section 7).
/// </remarks>
public sealed record ListCollectionRolesQuery : IQuery<IReadOnlyList<CollectionRoleListItem>>;

/// <summary>Flat read row for one collection role.</summary>
public sealed record CollectionRoleListItem(Guid Id, string Name, string? Description);

internal sealed class ListCollectionRolesQueryHandler
    : BaseDataAccess, IQueryHandler<ListCollectionRolesQuery, IReadOnlyList<CollectionRoleListItem>>
{
    private const string Sql =
        """
        SELECT
            r.id,
            r.name,
            r.description
        FROM configuration.collection_roles AS r
        WHERE r.company_id = @CompanyId
        ORDER BY r.name ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public ListCollectionRolesQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<CollectionRoleListItem>> HandleAsync(
        ListCollectionRolesQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        return (await connection.QueryAsync<CollectionRoleListItem>(
            new CommandDefinition(
                Sql,
                new { _tenantContext.CompanyId },
                cancellationToken: cancellationToken))).AsList();
    }
}
