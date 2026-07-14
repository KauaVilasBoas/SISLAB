using SISLAB.Modules.Identity.Infrastructure.Authorization;

namespace SISLAB.Api.Tests.Authorization;

/// <summary>
/// Anti-drift tests for the pt-BR permission display-name catalogue (<see cref="PermissionDisplayNames"/>),
/// the labels <see cref="PermissionDisplayNameSeeder"/> stamps onto <c>"Lumen"."Permission"."DisplayName"</c>
/// after discovery. They bind the catalogue 1:1 to the real controller actions discovered from the module
/// Application assemblies (<see cref="ControllerActionCatalog"/>), exactly like
/// <see cref="PermissionCatalogDriftTests"/> guards the permission-code constants.
///
/// <para>The guarantee: <b>every</b> code that carries a pt-BR label maps to a real
/// <c>&lt;Controller&gt;.&lt;Action&gt;</c> (no orphaned label), and <b>every</b> permission-gated endpoint
/// (<c>[RequirePermission]</c>) carries a pt-BR label (no gated endpoint shows the raw code in the UI). So a
/// newly gated endpoint added without a Portuguese label — or a renamed/removed action leaving a dangling
/// label — breaks the build until the catalogue is updated.</para>
///
/// <para>Note: a few catalogued codes (the read-only Audit endpoints) are labelled for completeness but are
/// not <c>[RequirePermission]</c>-gated; the seeder simply no-ops on them. They must still map to a real
/// controller action, which the first test enforces.</para>
/// </summary>
public sealed class PermissionDisplayNameCatalogTests
{
    /// <summary>
    /// Every code with a pt-BR label must correspond to a real controller action's computed permission code.
    /// No orphaned labels (a renamed/removed action would leave a dangling entry).
    /// </summary>
    [Fact]
    public void EveryDisplayNameCode_MapsTo_ARealControllerAction()
    {
        HashSet<string> realCodes = ControllerActionCatalog.All
            .Select(action => action.PermissionCode)
            .ToHashSet();

        List<string> orphaned = PermissionDisplayNames.ByCode.Keys
            .Where(code => !realCodes.Contains(code))
            .ToList();

        Assert.True(orphaned.Count == 0,
            "pt-BR display-name codes with no matching controller.action: " + string.Join(", ", orphaned));
    }

    /// <summary>
    /// Every permission-gated endpoint (<c>[RequirePermission]</c>) must have a pt-BR label, so the
    /// profile-management UI never falls back to the raw <c>Controller.Action</c> code. A new gated endpoint
    /// without a Portuguese label breaks this test.
    /// </summary>
    [Fact]
    public void EveryGatedEndpoint_HasA_DisplayName()
    {
        List<string> unlabelled = ControllerActionCatalog.All
            .Where(action => action.HasRequirePermission)
            .Select(action => action.PermissionCode)
            .Distinct()
            .Where(code => !PermissionDisplayNames.ByCode.ContainsKey(code))
            .ToList();

        Assert.True(unlabelled.Count == 0,
            "Permission-gated endpoints missing a pt-BR display name: " + string.Join(", ", unlabelled));
    }

    /// <summary>Every catalogued label must be a non-blank string — an empty label would be worse than the code.</summary>
    [Fact]
    public void EveryDisplayName_IsNonBlank()
    {
        List<string> blank = PermissionDisplayNames.ByCode
            .Where(entry => string.IsNullOrWhiteSpace(entry.Value))
            .Select(entry => entry.Key)
            .ToList();

        Assert.True(blank.Count == 0,
            "Permission codes with a blank pt-BR display name: " + string.Join(", ", blank));
    }

    /// <summary>Sanity: the catalogue must not be empty and discovery must have found gated endpoints.</summary>
    [Fact]
    public void Catalogue_And_Discovery_AreNotEmpty()
    {
        Assert.NotEmpty(PermissionDisplayNames.ByCode);
        Assert.Contains(ControllerActionCatalog.All, action => action.HasRequirePermission);
    }
}
