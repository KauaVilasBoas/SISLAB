using System.Reflection;
using System.Text.RegularExpressions;
using SISLAB.Modules.Inventory.Application;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Tests.Application.Equipments.Queries;

/// <summary>
/// Static tenant-scoping and derivation guard (card [E6] #66) for the EquipmentRead Dapper handlers. Like the
/// StockRead guard, the read side has NO EF global query filter, so every tenant-scoped SELECT must carry its
/// own <c>WHERE company_id = @CompanyId</c> (defense-in-depth, section 7). This guard also pins the #66
/// domain rules that live in SQL: null <c>next_calibration</c> (n/a / no planned date) is ignored, and the
/// overdue condition is <c>next_calibration &lt; @Today</c> against the handler-supplied clock. Cheap,
/// always-on tripwires that fail the build the moment the query drifts, without a live database.
/// </summary>
public sealed class EquipmentReadTenantFilterGuardTests
{
    private const string TenantPredicate = "company_id = @CompanyId";

    /// <summary>The EquipmentRead Dapper handlers that must be tenant-scoped. Pinned so a rename breaks the guard.</summary>
    private static readonly IReadOnlySet<string> ExpectedHandlerTypes = new HashSet<string>
    {
        "ListOverdueCalibrationEquipmentQueryHandler", // #66 overdue calibration scan
        "ListEquipmentQueryHandler",                   // #27 equipment listing
        "GetEquipmentDetailQueryHandler"               // #27 equipment detail
    };

    private static readonly Assembly ApplicationAssembly = typeof(InventoryModule).Assembly;

    [Fact]
    public void Discovery_matches_the_expected_handler_set_exactly()
    {
        IReadOnlySet<string> discovered = DiscoverEquipmentReadHandlers()
            .Select(handler => handler.Name)
            .ToHashSet();

        Assert.True(
            discovered.SetEquals(ExpectedHandlerTypes),
            "The reflected EquipmentRead Dapper handlers diverged from the expected set. " +
            $"Missing: [{string.Join(", ", ExpectedHandlerTypes.Except(discovered))}]. " +
            $"Unexpected: [{string.Join(", ", discovered.Except(ExpectedHandlerTypes))}]. " +
            "Update ExpectedHandlerTypes if a handler was intentionally added/renamed/removed.");
    }

    [Fact]
    public void Every_equipment_read_handler_scopes_its_sql_to_the_active_company()
    {
        foreach (Type handler in DiscoverEquipmentReadHandlers())
        {
            string normalized = NormalizeWhitespace(ReadSqlConstant(handler));

            Assert.True(
                normalized.Contains(TenantPredicate, StringComparison.OrdinalIgnoreCase),
                $"{handler.Name} has a Dapper SQL without the mandatory tenant filter '{TenantPredicate}'.");
        }
    }

    [Fact]
    public void The_overdue_calibration_query_ignores_null_next_calibration_and_derives_the_overdue_condition()
    {
        Type handler = DiscoverEquipmentReadHandlers()
            .Single(t => t.Name == "ListOverdueCalibrationEquipmentQueryHandler");

        string normalized = NormalizeWhitespace(ReadSqlConstant(handler));

        // "n/a" and "no planned next date" equipment must never appear.
        Assert.Contains("next_calibration IS NOT NULL", normalized, StringComparison.OrdinalIgnoreCase);
        // Overdue is derived against the handler-supplied @Today, not the DB clock.
        Assert.Contains("next_calibration < @Today", normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<Type> DiscoverEquipmentReadHandlers()
        => ApplicationAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.Namespace == "SISLAB.Modules.Inventory.Application.Equipments.Queries")
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
