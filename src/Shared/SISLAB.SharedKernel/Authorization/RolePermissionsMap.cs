namespace SISLAB.SharedKernel.Authorization;

/// <summary>
/// Maps each business <see cref="Role"/> to the set of write permission codes it grants. This is the
/// single source of truth used to provision the Lumen authorization Profiles per company (card #77d):
/// one Profile per Role, each seeded with the permissions returned here.
///
/// <para>Only <b>write</b> permissions are mapped. Reads (GET) are gated by authentication alone —
/// any member, including <see cref="Role.ReadOnly"/>, may read — so they never appear here.</para>
///
/// <para>Design guarantees (covered by tests): <see cref="Role.Coordinator"/> is a strict superset of
/// every other role (it grants all write permissions), and <see cref="Role.ReadOnly"/> grants none.</para>
/// </summary>
public static class RolePermissionsMap
{
    // Lazy initialization avoids any dependency on the textual order of static members: whichever
    // member is touched first triggers the shared build, and the union is computed once inside it.
    private static readonly Lazy<Built> Lazy = new(Build);

    /// <summary>The complete set of write permission codes across every module.</summary>
    public static IReadOnlySet<string> AllWritePermissions => Lazy.Value.AllWritePermissions;

    /// <summary>Returns the write permission codes granted to <paramref name="role"/>.</summary>
    public static IReadOnlySet<string> ForRole(Role role) => Lazy.Value.Map[role];

    private static Built Build()
    {
        IReadOnlySet<string> allWritePermissions = Union(
            InventoryPermissions.All,
            NotificationsPermissions.All,
            ConfigurationPermissions.All,
            AuditPermissions.All,
            CompanyMembersPermissions.All);

        // Coordinator: full control over the company — every write permission that exists.
        IReadOnlySet<string> coordinator = allWritePermissions;

        // Researcher: operates the lab (stock, equipment, partners, notifications), but neither
        // administers members/roles nor edits the company's reference-data configuration.
        IReadOnlySet<string> researcher = Union(
            InventoryPermissions.All,
            NotificationsPermissions.All);

        // ModuleManager: write access delegated to the module(s) under their responsibility. Modelled
        // here as inventory operations plus the per-tenant configuration of those modules, without
        // company-wide member administration.
        IReadOnlySet<string> moduleManager = Union(
            InventoryPermissions.All,
            ConfigurationPermissions.All,
            NotificationsPermissions.All);

        // Operator: day-to-day inventory operations with a narrower surface than a Researcher —
        // stock movements and acting on their own alerts, but no equipment/partner administration.
        IReadOnlySet<string> operator_ = Union(
            new HashSet<string>
            {
                InventoryPermissions.Stock.RegisterEntry,
                InventoryPermissions.Stock.RegisterConsumption,
                InventoryPermissions.Stock.RegisterCount
            },
            NotificationsPermissions.All);

        // ReadOnly: may view data but perform no write operation.
        IReadOnlySet<string> readOnly = new HashSet<string>();

        Dictionary<Role, IReadOnlySet<string>> map = new()
        {
            [Role.Coordinator] = coordinator,
            [Role.Researcher] = researcher,
            [Role.ModuleManager] = moduleManager,
            [Role.Operator] = operator_,
            [Role.ReadOnly] = readOnly
        };

        return new Built(map, allWritePermissions);
    }

    private static IReadOnlySet<string> Union(params IReadOnlySet<string>[] sets)
    {
        HashSet<string> union = [];
        foreach (IReadOnlySet<string> set in sets)
            union.UnionWith(set);

        return union;
    }

    /// <summary>Immutable result of building the map once.</summary>
    private sealed record Built(
        IReadOnlyDictionary<Role, IReadOnlySet<string>> Map,
        IReadOnlySet<string> AllWritePermissions);
}
