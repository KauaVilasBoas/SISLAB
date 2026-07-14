namespace SISLAB.Api.Tests.Authorization;

/// <summary>
/// Architecture guard (card [E12] #95): every write endpoint across the SISLAB modules must be gated by
/// Lumen's <c>[RequirePermission]</c>. A write endpoint is any public controller action carrying an
/// <c>[HttpPost]</c>, <c>[HttpPut]</c>, <c>[HttpDelete]</c> or <c>[HttpPatch]</c> attribute
/// (see <see cref="ControllerActionCatalog"/>, which classifies actions by their HTTP verbs).
///
/// <para>This is a method-level attribute check, which ArchUnitNET cannot express (its predicates operate on
/// types, not on the attributes of individual methods). It therefore lives here as a reflection test over the
/// real controllers of every module's Application assembly — the same discovery the permission-catalogue
/// drift tests use — so a new write endpoint that forgets <c>[RequirePermission]</c> breaks the build.</para>
///
/// <para>Reads (GET) are intentionally excluded: they are authenticated (<c>[Authorize]</c> on the controller)
/// but not permission-gated — any member, including <c>ReadOnly</c>, may read (see <c>RolePermissionsMap</c>).</para>
/// </summary>
public sealed class WriteEndpointPermissionTests
{
    /// <summary>
    /// Every write action (POST/PUT/DELETE/PATCH) must carry <c>[RequirePermission]</c> so Lumen materializes
    /// its permission code and enforces it. An undecorated write endpoint would be reachable by any
    /// authenticated member regardless of role — a privilege-escalation hole this test forbids.
    /// </summary>
    [Fact]
    public void EveryWriteEndpoint_MustHave_RequirePermission()
    {
        List<string> undecorated = ControllerActionCatalog.Writes
            .Where(action => !action.HasRequirePermission)
            .Select(action => action.ToString())
            .ToList();

        Assert.True(undecorated.Count == 0,
            "Write endpoints missing [RequirePermission] (privilege-escalation risk): " +
            string.Join(", ", undecorated));
    }

    /// <summary>Sanity: the discovery must actually have found write endpoints (guards a silent empty scan).</summary>
    [Fact]
    public void Discovery_ShouldFind_WriteEndpoints()
    {
        Assert.NotEmpty(ControllerActionCatalog.Writes);
    }
}
