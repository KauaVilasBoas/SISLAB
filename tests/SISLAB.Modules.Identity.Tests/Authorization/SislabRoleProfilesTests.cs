using SISLAB.Modules.Identity.Infrastructure.Authorization;
using SISLAB.SharedKernel.Authorization;

namespace SISLAB.Modules.Identity.Tests.Authorization;

/// <summary>
/// Guards the profile-naming contract behind the Role→Lumen-Profile translation (card [E12] #77d).
///
/// <para>SISLAB provisions one Lumen profile per business <see cref="Role"/>, named through
/// <see cref="SislabRoleProfiles"/>; the company scope lives on the <c>UserProfile</c> assignment, not
/// on the profile (see the class remarks). These tests pin the names as a stable, collision-free contract
/// — renaming a value would orphan already-provisioned profiles — and confirm each role maps to a
/// distinct profile whose permission set comes from <see cref="RolePermissionsMap"/>.</para>
/// </summary>
public sealed class SislabRoleProfilesTests
{
    [Fact]
    public void NameFor_EveryRole_IsPrefixedAndDistinct()
    {
        string[] names = SislabRoleProfiles.AllRoles
            .Select(SislabRoleProfiles.NameFor)
            .ToArray();

        Assert.All(names, n => Assert.StartsWith(SislabRoleProfiles.NamePrefix, n));
        Assert.Equal(names.Length, names.Distinct().Count());
    }

    [Fact]
    public void NameFor_DoesNotCollideWith_LumenSystemProfileNames()
    {
        // Lumen ships "Administrator" and "User" system profiles — SISLAB role profiles must not clash.
        string[] names = SislabRoleProfiles.AllRoles.Select(SislabRoleProfiles.NameFor).ToArray();

        Assert.DoesNotContain("Administrator", names);
        Assert.DoesNotContain("User", names);
    }

    [Theory]
    [InlineData(Role.Coordinator, "SISLAB.Coordinator")]
    [InlineData(Role.ReadOnly, "SISLAB.ReadOnly")]
    public void NameFor_IsStable(Role role, string expected)
    {
        Assert.Equal(expected, SislabRoleProfiles.NameFor(role));
    }

    [Fact]
    public void AllRoles_CoversEveryDeclaredRole()
    {
        Assert.Equal(Enum.GetValues<Role>().Length, SislabRoleProfiles.AllRoles.Count);
    }

    [Fact]
    public void CoordinatorProfile_Grants_MemberRoleManagement()
    {
        // The profile provisioner seeds each role profile from RolePermissionsMap. After card #77e wired
        // CompanyMembers.ChangeMemberRole into the write catalogue, only the Coordinator profile must carry
        // it — the permission that gates this very endpoint. This ties #77d's provisioning to #77e's action.
        Assert.Contains(CompanyMembersPermissions.ChangeMemberRole,
            RolePermissionsMap.ForRole(Role.Coordinator));

        foreach (Role role in SislabRoleProfiles.AllRoles.Where(r => r != Role.Coordinator))
        {
            Assert.DoesNotContain(CompanyMembersPermissions.ChangeMemberRole,
                RolePermissionsMap.ForRole(role));
        }
    }
}
