using System.Reflection;
using System.Text.RegularExpressions;
using SISLAB.Modules.Inventory.Application;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements.Queries;

/// <summary>
/// Static tenant-scoping guard (card [E4] #34, part A) for the read-side Dapper handlers of the StockRead
/// slice. The read side has NO EF global query filter, so every tenant-scoped SELECT must carry its own
/// <c>WHERE company_id = @CompanyId</c> (defense-in-depth, section 7). This test discovers those handlers by
/// reflection and asserts that predicate is present in the SQL of each one — a cheap, always-on tripwire that
/// fails the build the moment a handler ships (or is refactored into) a query without the mandatory filter,
/// without needing a live database.
/// </summary>
/// <remarks>
/// The reflection sweep is anchored by an explicit expected-handler list: the sweep and the list must agree
/// exactly, so the guard can never silently "pass empty" (e.g. if a handler is renamed, moved out of the
/// namespace, or its <c>Sql</c> field is renamed, the discovery diverges from the list and the test fails).
/// </remarks>
public sealed class StockReadTenantFilterGuardTests
{
    /// <summary>The mandatory tenant predicate every read-side handler SQL must contain (whitespace-normalized).</summary>
    private const string TenantPredicate = "company_id = @CompanyId";

    /// <summary>
    /// The names of the StockRead Dapper handlers that MUST be tenant-scoped (cards #29–#32). Pinned explicitly
    /// so a renamed/removed handler breaks the guard instead of silently shrinking the covered set.
    /// </summary>
    private static readonly IReadOnlySet<string> ExpectedHandlerTypes = new HashSet<string>
    {
        "ListStockItemsQueryHandler",        // #29 items by location/category
        "GetLocationsSummaryQueryHandler",   // #29 per-location summary
        "GetExpirySummaryQueryHandler",      // #30 expiry donut
        "ListExpiringItemsQueryHandler",     // #30 expiring items
        "ListItemsBelowMinimumQueryHandler", // #32 below-minimum list
        "GetBelowMinimumSummaryQueryHandler",// #32 below-minimum KPI
        "GetConsumptionReportQueryHandler",  // #31 consumption report
        "GetConsumptionSeriesQueryHandler",  // #31 consumption series
        "GetStockItemDetailQueryHandler"     // #35 single-item detail (public IInventoryApi boundary)
    };

    private static readonly Assembly ApplicationAssembly = typeof(InventoryModule).Assembly;

    [Fact]
    public void Discovery_matches_the_expected_handler_set_exactly()
    {
        IReadOnlySet<string> discovered = DiscoverStockReadHandlers()
            .Select(handler => handler.Name)
            .ToHashSet();

        Assert.True(
            discovered.SetEquals(ExpectedHandlerTypes),
            "The reflected StockRead Dapper handlers diverged from the expected set. " +
            $"Missing (expected but not found): [{string.Join(", ", ExpectedHandlerTypes.Except(discovered))}]. " +
            $"Unexpected (found but not listed): [{string.Join(", ", discovered.Except(ExpectedHandlerTypes))}]. " +
            "Update ExpectedHandlerTypes if a handler was intentionally added/renamed/removed.");
    }

    [Fact]
    public void Every_stock_read_handler_scopes_its_sql_to_the_active_company()
    {
        IReadOnlyList<Type> handlers = DiscoverStockReadHandlers();

        foreach (Type handler in handlers)
        {
            string sql = ReadSqlConstant(handler);
            string normalized = NormalizeWhitespace(sql);

            Assert.True(
                normalized.Contains(TenantPredicate, StringComparison.OrdinalIgnoreCase),
                $"{handler.Name} has a Dapper SQL that does not contain the mandatory tenant filter " +
                $"'{TenantPredicate}'. The read side has no EF global query filter, so every tenant-scoped " +
                "SELECT must keep 'WHERE company_id = @CompanyId' (section 7, defense-in-depth).");
        }
    }

    /// <summary>
    /// Finds the concrete <see cref="IQueryHandler{TQuery,TResult}"/> implementations of the StockRead slice
    /// (namespace-scoped) that own a Dapper <c>Sql</c> constant — the read-side handlers this guard covers.
    /// </summary>
    private static IReadOnlyList<Type> DiscoverStockReadHandlers()
        => ApplicationAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.Namespace == "SISLAB.Modules.Inventory.Application.StockMovements.Queries")
            .Where(ImplementsQueryHandler)
            .Where(HasSqlConstant)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToList();

    private static bool ImplementsQueryHandler(Type type)
        => type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>));

    private static bool HasSqlConstant(Type type)
        => FindSqlField(type) is not null;

    /// <summary>Reads the value of the handler's private const <c>Sql</c> string field.</summary>
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

    /// <summary>Collapses every run of whitespace to a single space so the predicate matches regardless of formatting.</summary>
    private static string NormalizeWhitespace(string sql)
        => Regex.Replace(sql, @"\s+", " ").Trim();
}
