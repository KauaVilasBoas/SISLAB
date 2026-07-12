using System.Reflection;
using SISLAB.Modules.Inventory.Contracts;
using SISLAB.Modules.Inventory.Contracts.Dtos;

namespace SISLAB.Modules.Inventory.Tests.Application.PublicApi;

/// <summary>
/// Reflection guard on the public boundary (card [E5] #35): no member of <see cref="IInventoryApi"/> or
/// of any Contracts DTO may expose a type from the Inventory Domain or from EF Core. This complements the
/// ArchUnit assembly-level rule with a member-level check — it fails the moment a method parameter,
/// return type or DTO property leaks an internal aggregate/value object or a persistence type across the
/// module fronteira, which primitives-only contracts must never do.
/// </summary>
public sealed class InventoryApiContractPurityTests
{
    // Namespaces that must never surface on the public boundary. The module's internals live under
    // ...Inventory.Domain/.Application/.Infrastructure; persistence types under Microsoft.EntityFrameworkCore.
    private static readonly string[] ForbiddenNamespacePrefixes =
    {
        "SISLAB.Modules.Inventory.Domain",
        "SISLAB.Modules.Inventory.Application",
        "SISLAB.Modules.Inventory.Infrastructure",
        "Microsoft.EntityFrameworkCore"
    };

    public static IEnumerable<object[]> DtoTypes()
    {
        yield return new object[] { typeof(StockItemSummaryDto) };
        yield return new object[] { typeof(StockBalanceDto) };
        yield return new object[] { typeof(ExpiringItemDto) };
        yield return new object[] { typeof(BelowMinimumItemDto) };
    }

    [Fact]
    public void IInventoryApi_members_expose_no_domain_or_ef_types()
    {
        foreach (MethodInfo method in typeof(IInventoryApi).GetMethods())
        {
            AssertTypeIsClean(UnwrapTask(method.ReturnType), $"{method.Name} return type");

            foreach (ParameterInfo parameter in method.GetParameters())
                AssertTypeIsClean(parameter.ParameterType, $"{method.Name} parameter '{parameter.Name}'");
        }
    }

    [Theory]
    [MemberData(nameof(DtoTypes))]
    public void Dto_properties_expose_no_domain_or_ef_types(Type dtoType)
    {
        foreach (PropertyInfo property in dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            AssertTypeIsClean(property.PropertyType, $"{dtoType.Name}.{property.Name}");
    }

    private static void AssertTypeIsClean(Type type, string context)
    {
        foreach (Type candidate in FlattenGenericArguments(type))
        {
            string? ns = candidate.Namespace;
            if (ns is null)
                continue;

            bool forbidden = ForbiddenNamespacePrefixes.Any(prefix =>
                ns.Equals(prefix, StringComparison.Ordinal) ||
                ns.StartsWith(prefix + ".", StringComparison.Ordinal));

            Assert.False(
                forbidden,
                $"{context} exposes forbidden type '{candidate.FullName}' across the module boundary.");
        }
    }

    /// <summary>Unwraps <c>Task&lt;T&gt;</c> to <c>T</c> so the awaited result type is inspected.</summary>
    private static Type UnwrapTask(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>)
            ? type.GetGenericArguments()[0]
            : type;

    /// <summary>
    /// Yields the type and, recursively, every generic argument it carries — so a leak hidden inside
    /// <c>IReadOnlyList&lt;Leak&gt;</c> or <c>Nullable&lt;Leak&gt;</c> is still caught.
    /// </summary>
    private static IEnumerable<Type> FlattenGenericArguments(Type type)
    {
        yield return type;

        if (!type.IsGenericType)
            yield break;

        foreach (Type argument in type.GetGenericArguments())
            foreach (Type nested in FlattenGenericArguments(argument))
                yield return nested;
    }
}
