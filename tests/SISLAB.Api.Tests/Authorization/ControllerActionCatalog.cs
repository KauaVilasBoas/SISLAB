using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace SISLAB.Api.Tests.Authorization;

/// <summary>
/// Reflects over every MVC controller shipped by the SISLAB modules and materializes each public action
/// as a <see cref="ControllerAction"/>, computing the Lumen permission code exactly as Lumen 1.1.0 does:
/// <c>&lt;Controller-without-suffix&gt;.&lt;ActionName&gt;</c>, where <c>ActionName</c> honours an explicit
/// <see cref="ActionNameAttribute"/> and otherwise falls back to the method name.
///
/// <para>Used by the permission-catalogue drift test (card #77b) and the write-endpoint coverage test
/// (card #77c). Discovering controllers by their conventional <c>Controller</c> suffix across the loaded
/// module Application assemblies mirrors how the ASP.NET controller feature provider finds them.</para>
/// </summary>
internal static class ControllerActionCatalog
{
    private static readonly string[] WriteHttpMethods = ["POST", "PUT", "DELETE", "PATCH"];

    /// <summary>Marker types anchoring each module's Application assembly (where its controllers live).</summary>
    private static readonly Type[] ModuleAnchors =
    [
        typeof(SISLAB.Modules.Inventory.Application.InventoryModule),
        typeof(SISLAB.Modules.Identity.Application.IdentityModule),
        typeof(SISLAB.Modules.Notifications.Application.NotificationsModule),
        typeof(SISLAB.Modules.Configuration.Application.ConfigurationModule),
        typeof(SISLAB.Modules.Audit.Application.AuditModule),
        typeof(SISLAB.Modules.Experiments.Application.ExperimentsModule)
    ];

    public static IReadOnlyList<ControllerAction> All { get; } = Discover();

    public static IEnumerable<ControllerAction> Writes => All.Where(a => a.IsWrite);

    public static IEnumerable<ControllerAction> Reads => All.Where(a => !a.IsWrite);

    private static IReadOnlyList<ControllerAction> Discover()
    {
        List<ControllerAction> actions = [];

        IEnumerable<Type> controllers = ModuleAnchors
            .Select(anchor => anchor.Assembly)
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsController);

        foreach (Type controller in controllers)
        {
            string prefix = controller.Name[..^"Controller".Length];

            foreach (MethodInfo method in controller.GetMethods(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName || !IsAction(method, out string[] httpMethods))
                    continue;

                string actionName = method.GetCustomAttribute<ActionNameAttribute>()?.Name ?? method.Name;
                bool isWrite = httpMethods.Any(m => WriteHttpMethods.Contains(m));

                actions.Add(new ControllerAction(
                    controller,
                    method,
                    PermissionCode: $"{prefix}.{actionName}",
                    IsWrite: isWrite));
            }
        }

        return actions;
    }

    private static bool IsController(Type type) =>
        type is { IsClass: true, IsAbstract: false } &&
        type.Name.EndsWith("Controller", StringComparison.Ordinal) &&
        typeof(ControllerBase).IsAssignableFrom(type);

    private static bool IsAction(MethodInfo method, out string[] httpMethods)
    {
        List<string> verbs = [];
        foreach (IActionHttpMethodProvider attribute in
                 method.GetCustomAttributes().OfType<IActionHttpMethodProvider>())
        {
            verbs.AddRange(attribute.HttpMethods);
        }

        httpMethods = verbs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return httpMethods.Length > 0;
    }
}

/// <summary>A single controller action with its computed Lumen permission code and write classification.</summary>
internal sealed record ControllerAction(
    Type Controller,
    MethodInfo Method,
    string PermissionCode,
    bool IsWrite)
{
    public bool HasRequirePermission =>
        Method.GetCustomAttributes()
            .Any(a => a.GetType().Name == "RequirePermissionAttribute");

    /// <summary>
    /// True when the action (or its controller) is explicitly <c>[AllowAnonymous]</c>. Such an endpoint has no
    /// authenticated principal or active tenant, so it cannot be permission-gated — it is excluded from the
    /// write-permission requirement. Public self-service company signup (card [E12] #75a) is the only such write.
    /// </summary>
    public bool IsAnonymous =>
        Method.GetCustomAttributes().Any(a => a.GetType().Name == "AllowAnonymousAttribute")
        || Controller.GetCustomAttributes(inherit: true)
            .Any(a => a.GetType().Name == "AllowAnonymousAttribute");

    public override string ToString() => $"{Controller.Name}.{Method.Name} ({PermissionCode})";
}
