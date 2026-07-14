using SISLAB.Modules.Identity.Infrastructure.Authorization;

namespace SISLAB.Api.Tests.Authorization;

/// <summary>
/// Anti-drift tests for the SISLAB permission catalogue (<see cref="PermissionCatalog"/>), the groups and
/// permissions <see cref="LumenPermissionCatalogSeeder"/> seeds into <c>"Lumen"."PermissionGroup"</c> /
/// <c>"Lumen"."Permission"</c>. Since Lumen.Authorization 2.0.0 only validates the catalogue (it no longer
/// writes it) and logs a warning for any <c>[RequirePermission]</c> code missing from the database, these
/// tests bind the seeded catalogue 1:1 to the real gated controller actions discovered from the module
/// Application assemblies (<see cref="ControllerActionCatalog"/>).
///
/// <para>The guarantee: <b>every</b> permission-gated endpoint has a seeded catalogue entry (so the boot-time
/// validation never warns and no member can be locked out by a missing permission row), and <b>every</b>
/// catalogued code maps to a real <c>&lt;Controller&gt;.&lt;Action&gt;</c> (no orphaned entry). A newly gated
/// endpoint added without a catalogue entry — or a renamed/removed action leaving a dangling entry — breaks the
/// build until the catalogue is updated.</para>
/// </summary>
public sealed class PermissionCatalogSeedTests
{
    private static IEnumerable<PermissionCatalog.Permission> AllPermissions =>
        PermissionCatalog.Groups.SelectMany(g => g.Permissions);

    /// <summary>
    /// Every permission-gated endpoint (<c>[RequirePermission]</c>) must have a seeded catalogue entry, so
    /// Lumen's Validate-mode boot check never warns and the profile UI can offer the permission.
    /// </summary>
    [Fact]
    public void EveryGatedEndpoint_IsSeededIn_TheCatalogue()
    {
        HashSet<string> seededCodes = AllPermissions.Select(p => p.Code).ToHashSet();

        List<string> missing = ControllerActionCatalog.All
            .Where(action => action.HasRequirePermission)
            .Select(action => action.PermissionCode)
            .Distinct()
            .Where(code => !seededCodes.Contains(code))
            .ToList();

        Assert.True(missing.Count == 0,
            "Permission-gated endpoints missing a seeded catalogue entry: " + string.Join(", ", missing));
    }

    /// <summary>
    /// Every catalogued code must correspond to a real controller action's computed permission code —
    /// no orphaned entries (a renamed/removed action would leave a dangling code in the seed).
    /// </summary>
    [Fact]
    public void EveryCatalogueCode_MapsTo_ARealControllerAction()
    {
        HashSet<string> realCodes = ControllerActionCatalog.All
            .Select(action => action.PermissionCode)
            .ToHashSet();

        List<string> orphaned = AllPermissions
            .Select(p => p.Code)
            .Where(code => !realCodes.Contains(code))
            .ToList();

        Assert.True(orphaned.Count == 0,
            "Catalogue codes with no matching controller.action: " + string.Join(", ", orphaned));
    }

    /// <summary>Deterministic identity is the whole point of the idempotent seed: no duplicate ids or codes.</summary>
    [Fact]
    public void CatalogueIds_And_Codes_AreUnique()
    {
        List<Guid> groupIds = PermissionCatalog.Groups.Select(g => g.Id).ToList();
        List<Guid> permissionIds = AllPermissions.Select(p => p.Id).ToList();
        List<string> codes = AllPermissions.Select(p => p.Code).ToList();

        Guid[] allIds = groupIds.Concat(permissionIds).ToArray();

        Assert.Equal(allIds.Length, allIds.Distinct().Count());
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.DoesNotContain(allIds, id => id == Guid.Empty);
    }

    /// <summary>Every group and permission must carry a non-blank name/label — a blank would be worse than the code.</summary>
    [Fact]
    public void EveryNameAndDisplayName_IsNonBlank()
    {
        Assert.All(PermissionCatalog.Groups, group =>
        {
            Assert.False(string.IsNullOrWhiteSpace(group.Name));
            Assert.False(string.IsNullOrWhiteSpace(group.Description));
            Assert.NotEmpty(group.Permissions);
        });

        Assert.All(AllPermissions, permission =>
        {
            Assert.False(string.IsNullOrWhiteSpace(permission.Code));
            Assert.False(string.IsNullOrWhiteSpace(permission.DisplayName));
        });
    }
}
