using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Application.ItemCategories;

/// <summary>
/// Read-side query (card [E12] #76) that lists the active company's item categories for the configuration
/// screen, ordered by name. It reads <c>configuration.item_categories</c> via Dapper — never the write
/// DbContext — and projects a flat row the UI renders directly (the aliases are joined into a single string).
/// The catalogue is small, so the list is returned whole (no pagination).
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from
/// the request, and the SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global
/// query filter, so the tenant guard is explicit (defense-in-depth, section 7).
/// </remarks>
public sealed record ListItemCategoriesQuery : IQuery<IReadOnlyList<ItemCategoryListItem>>;

/// <summary>Flat read row for the item-category configuration list.</summary>
public sealed record ItemCategoryListItem(Guid Id, string Name, string Aliases, bool IsControlled);

internal sealed class ListItemCategoriesQueryHandler
    : BaseDataAccess, IQueryHandler<ListItemCategoriesQuery, IReadOnlyList<ItemCategoryListItem>>
{
    private const string Sql =
        """
        SELECT
            c.id,
            c.name,
            c.aliases,
            c.is_controlled AS iscontrolled
        FROM configuration.item_categories AS c
        WHERE c.company_id = @CompanyId
        ORDER BY c.name ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public ListItemCategoriesQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<ItemCategoryListItem>> HandleAsync(
        ListItemCategoriesQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        IReadOnlyList<ItemCategoryListItem> rows = (await connection.QueryAsync<ItemCategoryListItem>(
            new CommandDefinition(
                Sql,
                new { _tenantContext.CompanyId },
                cancellationToken: cancellationToken))).AsList();

        return rows;
    }
}
