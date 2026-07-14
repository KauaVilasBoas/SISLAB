using SISLAB.SharedKernel.Authorization;

namespace SISLAB.SharedKernel.Tests.Authorization;

/// <summary>
/// Guards the Role→permissions map (card [E12] #77b): the invariants the provisioning of Lumen
/// Profiles (card #77d) relies upon. All five roles are covered.
/// </summary>
public sealed class RolePermissionsMapTests
{
    [Fact]
    public void Coordinator_ShouldGrant_AllWritePermissions()
    {
        IReadOnlySet<string> coordinator = RolePermissionsMap.ForRole(Role.Coordinator);

        Assert.Equal(RolePermissionsMap.AllWritePermissions, coordinator);
    }

    [Theory]
    [InlineData(Role.Researcher)]
    [InlineData(Role.ModuleManager)]
    [InlineData(Role.Operator)]
    [InlineData(Role.ReadOnly)]
    public void Coordinator_ShouldBeSuperset_OfEveryOtherRole(Role otherRole)
    {
        IReadOnlySet<string> coordinator = RolePermissionsMap.ForRole(Role.Coordinator);
        IReadOnlySet<string> other = RolePermissionsMap.ForRole(otherRole);

        Assert.True(coordinator.IsSupersetOf(other),
            $"Coordinator must grant every permission that {otherRole} grants.");
    }

    [Fact]
    public void ReadOnly_ShouldGrant_NoWritePermission()
    {
        IReadOnlySet<string> readOnly = RolePermissionsMap.ForRole(Role.ReadOnly);

        Assert.Empty(readOnly);
    }

    [Fact]
    public void EveryRole_ShouldGrant_OnlyKnownWritePermissions()
    {
        foreach (Role role in Enum.GetValues<Role>())
        {
            IReadOnlySet<string> granted = RolePermissionsMap.ForRole(role);
            Assert.True(RolePermissionsMap.AllWritePermissions.IsSupersetOf(granted),
                $"{role} grants a permission that is not in the global write-permission catalogue.");
        }
    }

    [Fact]
    public void AllWritePermissions_ShouldEqual_UnionOfModuleCatalogues()
    {
        HashSet<string> expected =
        [
            .. InventoryPermissions.All,
            .. NotificationsPermissions.All,
            .. ConfigurationPermissions.All,
            .. AuditPermissions.All,
            .. CompanyMembersPermissions.All
        ];

        Assert.Equal(expected, RolePermissionsMap.AllWritePermissions);
    }

    [Fact]
    public void EveryRole_ShouldBeMapped()
    {
        foreach (Role role in Enum.GetValues<Role>())
        {
            // ForRole must not throw for any declared role (dictionary completeness).
            IReadOnlySet<string> granted = RolePermissionsMap.ForRole(role);
            Assert.NotNull(granted);
        }
    }

    [Fact]
    public void Operator_ShouldBeSubsetOf_Researcher()
    {
        // The Operator's inventory surface is intentionally narrower than the Researcher's.
        IReadOnlySet<string> op = RolePermissionsMap.ForRole(Role.Operator);
        IReadOnlySet<string> researcher = RolePermissionsMap.ForRole(Role.Researcher);

        Assert.True(researcher.IsSupersetOf(op));
    }
}
