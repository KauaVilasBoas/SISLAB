using SISLAB.SharedKernel.Authorization;

namespace SISLAB.Api.Tests.Authorization;

/// <summary>
/// Anti-drift tests for the permission catalogue (card [E12] #77b). They tie the <c>*Permissions</c>
/// constants to the real controller actions so a renamed controller or action breaks the build until
/// the catalogue is updated — keeping the constants a faithful mirror of the codes Lumen materializes
/// and enforces via <c>[RequirePermission]</c>.
///
/// <para>The catalogue is the union of every module's write-permission set (<c>*Permissions.All</c>).
/// SISLAB no longer maps roles to permissions — which members hold a code in a company is owned by
/// Lumen (profiles assigned to the user, scoped to the company). These tests only guard that the
/// catalogue of <i>codes</i> stays in sync with the real controller actions.</para>
/// </summary>
public sealed class PermissionCatalogDriftTests
{
    /// <summary>The union of every module's write-permission catalogue — the codes Lumen materializes.</summary>
    private static readonly IReadOnlySet<string> AllWritePermissions = new HashSet<string>(
    [
        .. InventoryPermissions.All,
        .. NotificationsPermissions.All,
        .. ConfigurationPermissions.All,
        .. AuditPermissions.All,
        .. CompanyMembersPermissions.All
    ]);

    /// <summary>
    /// Every write-permission code in the module catalogues must correspond to a real write action
    /// (POST/PUT/DELETE/PATCH) discovered on a controller. No orphaned constants.
    /// </summary>
    [Fact]
    public void EveryCatalogueWritePermission_MapsTo_ARealWriteAction()
    {
        HashSet<string> realWriteCodes = ControllerActionCatalog.Writes
            .Select(a => a.PermissionCode)
            .ToHashSet();

        List<string> orphaned = AllWritePermissions
            .Where(code => !realWriteCodes.Contains(code))
            .ToList();

        Assert.True(orphaned.Count == 0,
            "Catalogue write permissions with no matching controller.action: " +
            string.Join(", ", orphaned));
    }

    /// <summary>
    /// Conversely, every real write action's code must be present in the catalogue — so a new write
    /// endpoint cannot silently escape the permission catalogue Lumen enforces.
    /// </summary>
    [Fact]
    public void EveryRealWriteAction_IsPresentIn_TheCatalogue()
    {
        List<string> uncatalogued = ControllerActionCatalog.Writes
            .Select(a => a.PermissionCode)
            .Where(code => !AllWritePermissions.Contains(code))
            .Distinct()
            .ToList();

        Assert.True(uncatalogued.Count == 0,
            "Write actions missing a catalogue constant: " +
            string.Join(", ", uncatalogued));
    }

    /// <summary>
    /// The read codes catalogued for Audit and CompanyMembers must also match real read actions —
    /// they are documentation-only but still must not drift.
    /// </summary>
    [Fact]
    public void CataloguedReadCodes_MapTo_RealReadActions()
    {
        HashSet<string> realReadCodes = ControllerActionCatalog.Reads
            .Select(a => a.PermissionCode)
            .ToHashSet();

        string[] cataloguedReadCodes =
        [
            AuditPermissions.List,
            AuditPermissions.Export,
            CompanyMembersPermissions.ListMembers,
            CompanyMembersPermissions.CheckRemovalEligibility
        ];

        List<string> drifted = cataloguedReadCodes
            .Where(code => !realReadCodes.Contains(code))
            .ToList();

        Assert.True(drifted.Count == 0,
            "Catalogued read codes with no matching read action: " + string.Join(", ", drifted));
    }

    /// <summary>Sanity: the catalogue must actually have discovered controllers (guards a silent empty scan).</summary>
    [Fact]
    public void Catalog_ShouldDiscover_ControllerActions()
    {
        Assert.NotEmpty(ControllerActionCatalog.Writes);
        Assert.NotEmpty(ControllerActionCatalog.Reads);
    }
}
