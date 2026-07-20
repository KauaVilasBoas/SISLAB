using System.Reflection;
using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SISLAB.ArchitectureTests;

/// <summary>
/// Locks in the RBAC boundary (card [E12] #77c/#95): every state-changing HTTP endpoint MUST be
/// permission-gated by Lumen's <see cref="RequirePermissionAttribute"/>, so no unguarded write can
/// ever reach <c>main</c>. Reads (GET) are intentionally out of scope — they are authenticated by the
/// controller-level <c>[Authorize]</c>/fallback policy but not permission-gated.
///
/// This rule is expressed with reflection rather than ArchUnitNET's fluent API on purpose: the rule is
/// "a method carrying a write-verb attribute must also carry (or inherit from its controller) a
/// specific custom attribute", and precise method-level custom-attribute assertions are exactly what
/// reflection models cleanly. The test project already references every controller-hosting assembly,
/// so the types load without extra wiring.
///
/// <see cref="RequirePermissionAttribute"/> is <c>Inherited = true</c> and targets both Method and
/// Class, so decorating the controller class satisfies the rule for all its actions — hence the
/// method-OR-class check below mirrors Lumen's own enforcement semantics.
///
/// <para><b>Anonymous carve-out:</b> an action explicitly marked <c>[AllowAnonymous]</c> is exempt. Such an
/// endpoint has no authenticated principal and no active tenant, so it <i>cannot</i> be permission-gated —
/// requiring <c>[RequirePermission]</c> there would be nonsensical. The only write in this category is public
/// self-service company signup (card [E12] #75a). Everything else must still be gated.</para>
/// </summary>
public sealed class WriteEndpointAuthorizationTests
{
    /// <summary>
    /// The assemblies that host MVC controllers — one per module that exposes an HTTP surface. Lumen's
    /// discovery scanner only sees MVC <c>ControllerActionDescriptor</c>s, so every permission-gated
    /// write endpoint lives here as a controller action.
    /// </summary>
    private static readonly Assembly[] ControllerAssemblies =
    [
        typeof(Modules.Inventory.Application.InventoryModule).Assembly,
        typeof(Modules.Configuration.Application.ConfigurationModule).Assembly,
        typeof(Modules.Notifications.Application.NotificationsModule).Assembly,
        typeof(Modules.Identity.Application.IdentityModule).Assembly,
        typeof(Modules.Experiments.Application.ExperimentsModule).Assembly,
        typeof(Modules.Agenda.Application.AgendaModule).Assembly,
    ];

    /// <summary>The MVC verb attributes that denote a state-changing action.</summary>
    private static readonly Type[] WriteVerbAttributes =
    [
        typeof(HttpPostAttribute),
        typeof(HttpPutAttribute),
        typeof(HttpDeleteAttribute),
        typeof(HttpPatchAttribute),
    ];

    [Fact]
    public void Every_write_action_on_every_controller_requires_a_permission()
    {
        MethodInfo[] writeActions = ControllerAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsController)
            .SelectMany(controller => controller.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(IsWriteAction)
            .ToArray();

        // Guard against a vacuous pass: if a module rename or missing assembly reference silently
        // dropped every controller from discovery, the rule would trivially hold while protecting
        // nothing. The solution ships many write endpoints, so a low count means the scan is broken.
        Assert.True(
            writeActions.Length >= 20,
            $"Expected the controller scan to discover the solution's write endpoints, but found only " +
            $"{writeActions.Length}. The RequirePermission rule would be vacuously true — check that " +
            "every controller-hosting assembly is still referenced and discoverable.");

        MethodInfo[] unguarded = writeActions
            .Where(action => !IsAnonymous(action) && !IsPermissionGated(action))
            .ToArray();

        Assert.True(
            unguarded.Length == 0,
            "The following write actions (POST/PUT/DELETE/PATCH) are missing [RequirePermission] on " +
            "the action or their controller, leaving a state-changing endpoint unguarded:" +
            Environment.NewLine +
            string.Join(
                Environment.NewLine,
                unguarded.Select(m => $"  - {m.DeclaringType!.FullName}.{m.Name}")));
    }

    private static bool IsController(Type type) =>
        type is { IsClass: true, IsAbstract: false }
        && typeof(ControllerBase).IsAssignableFrom(type);

    private static bool IsWriteAction(MethodInfo method) =>
        WriteVerbAttributes.Any(verb => method.GetCustomAttributes(verb, inherit: true).Length != 0);

    /// <summary>
    /// A write action is permission-gated when <see cref="RequirePermissionAttribute"/> sits on the
    /// action itself or is inherited from the declaring controller — the same method-or-class semantics
    /// Lumen's enforcement uses.
    /// </summary>
    private static bool IsPermissionGated(MethodInfo method) =>
        method.GetCustomAttribute<RequirePermissionAttribute>(inherit: true) is not null
        || method.DeclaringType!.GetCustomAttribute<RequirePermissionAttribute>(inherit: true) is not null;

    /// <summary>
    /// A write action is exempt when it (or its controller) is explicitly <c>[AllowAnonymous]</c>: an
    /// unauthenticated endpoint has no principal/tenant to permission-check. Public company signup is the
    /// only such write.
    /// </summary>
    private static bool IsAnonymous(MethodInfo method) =>
        method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null
        || method.DeclaringType!.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null;
}
