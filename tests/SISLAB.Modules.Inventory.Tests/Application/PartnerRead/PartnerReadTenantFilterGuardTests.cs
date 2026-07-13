using System.Reflection;
using System.Text.RegularExpressions;
using SISLAB.Modules.Inventory.Application;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Tests.Application.PartnerRead;

/// <summary>
/// Static tenant-scoping guard (card [E4] #28) for the read-side Dapper handlers of the PartnerRead slice. The
/// read side has NO EF global query filter, so every tenant-scoped SELECT must carry its own
/// <c>WHERE company_id = @CompanyId</c> (defense-in-depth, section 7). This test discovers those handlers by
/// reflection and asserts that predicate is present in the SQL of each one — a cheap, always-on tripwire that
/// fails the build the moment a handler ships (or is refactored into) a query without the mandatory filter,
/// without needing a live database. It mirrors <c>StockReadTenantFilterGuardTests</c> for the partners slice.
/// </summary>
/// <remarks>
/// The reflection sweep is anchored by an explicit expected-handler list: the sweep and the list must agree
/// exactly, so the guard can never silently "pass empty" (e.g. if a handler is renamed, moved out of the
/// namespace, or its <c>Sql</c> field is renamed, the discovery diverges from the list and the test fails).
/// </remarks>
public sealed class PartnerReadTenantFilterGuardTests
{
    private const string TenantPredicate = "company_id = @CompanyId";

    /// <summary>The PartnerRead Dapper handlers that must be tenant-scoped. Pinned so a rename breaks the guard.</summary>
    private static readonly IReadOnlySet<string> ExpectedHandlerTypes = new HashSet<string>
    {
        "ListPartnersQueryHandler",   // #28 partners listing
        "GetPartnerDetailQueryHandler" // #28 partner detail
    };

    private static readonly Assembly ApplicationAssembly = typeof(InventoryModule).Assembly;

    [Fact]
    public void Discovery_matches_the_expected_handler_set_exactly()
    {
        IReadOnlySet<string> discovered = DiscoverPartnerReadHandlers()
            .Select(handler => handler.Name)
            .ToHashSet();

        Assert.True(
            discovered.SetEquals(ExpectedHandlerTypes),
            "The reflected PartnerRead Dapper handlers diverged from the expected set. " +
            $"Missing: [{string.Join(", ", ExpectedHandlerTypes.Except(discovered))}]. " +
            $"Unexpected: [{string.Join(", ", discovered.Except(ExpectedHandlerTypes))}]. " +
            "Update ExpectedHandlerTypes if a handler was intentionally added/renamed/removed.");
    }

    [Fact]
    public void Every_partner_read_handler_scopes_its_sql_to_the_active_company()
    {
        foreach (Type handler in DiscoverPartnerReadHandlers())
        {
            string normalized = NormalizeWhitespace(ReadSqlConstant(handler));

            Assert.True(
                normalized.Contains(TenantPredicate, StringComparison.OrdinalIgnoreCase),
                $"{handler.Name} has a Dapper SQL without the mandatory tenant filter '{TenantPredicate}'. " +
                "The read side has no EF global query filter, so every tenant-scoped SELECT must keep " +
                "'WHERE company_id = @CompanyId' (section 7, defense-in-depth).");
        }
    }

    private static IReadOnlyList<Type> DiscoverPartnerReadHandlers()
        => ApplicationAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.Namespace == "SISLAB.Modules.Inventory.Application.Partners.Queries")
            .Where(ImplementsQueryHandler)
            .Where(HasSqlConstant)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToList();

    private static bool ImplementsQueryHandler(Type type)
        => type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>));

    private static bool HasSqlConstant(Type type) => FindSqlField(type) is not null;

    private static string ReadSqlConstant(Type handler)
    {
        FieldInfo field = FindSqlField(handler)
            ?? throw new InvalidOperationException($"{handler.Name} does not declare a string 'Sql' field.");
        return (string)field.GetValue(null)!;
    }

    private static FieldInfo? FindSqlField(Type type)
        => type.GetField("Sql", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public) is { } field
           && field.FieldType == typeof(string)
            ? field
            : null;

    private static string NormalizeWhitespace(string sql)
        => Regex.Replace(sql, @"\s+", " ").Trim();
}
