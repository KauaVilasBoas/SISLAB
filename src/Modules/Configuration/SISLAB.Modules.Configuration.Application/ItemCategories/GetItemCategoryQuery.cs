using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Application.ItemCategories;

/// <summary>
/// Read-side query (card [E12] #76) that resolves a single item category of the active company by id. It
/// reads <c>configuration.item_categories</c> via Dapper and returns <see langword="null"/> when no such
/// category exists for the tenant. Backs the <c>ILabConfiguration</c> port the Inventory module uses to
/// validate/resolve a stock item's category by value.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from
/// the request, and the SELECT keeps <c>WHERE company_id = @CompanyId</c> — so a caller can never resolve a
/// category from another tenant through this surface.
/// </remarks>
public sealed record GetItemCategoryQuery(Guid CategoryId) : IQuery<ItemCategoryView?>;

/// <summary>Flat read view of one item category, exposed to the module's adapter/consumers.</summary>
public sealed record ItemCategoryView(Guid Id, string Name, bool IsControlled);

internal sealed class GetItemCategoryQueryHandler
    : BaseDataAccess, IQueryHandler<GetItemCategoryQuery, ItemCategoryView?>
{
    private const string Sql =
        """
        SELECT
            c.id,
            c.name,
            c.is_controlled AS iscontrolled
        FROM configuration.item_categories AS c
        WHERE c.company_id = @CompanyId
          AND c.id = @CategoryId
        LIMIT 1;
        """;

    private readonly ITenantContext _tenantContext;

    public GetItemCategoryQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<ItemCategoryView?> HandleAsync(
        GetItemCategoryQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<ItemCategoryView?>(
            new CommandDefinition(
                Sql,
                new { _tenantContext.CompanyId, request.CategoryId },
                cancellationToken: cancellationToken));
    }
}
