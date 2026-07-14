using Lumen.Authorization.Application.Profiles.Create;
using Lumen.Authorization.Application.Profiles.SetPermissions;
using Lumen.Authorization.Application.Queries;
using Lumen.Authorization.Application.UserProfiles.Assign;
using Lumen.Authorization.Application.UserProfiles.Remove;
using SISLAB.Modules.Identity.Contracts.Authorization;
using SISLAB.Modules.Identity.Infrastructure.Authorization;

namespace SISLAB.Modules.Identity.Tests.Authorization;

/// <summary>
/// Proves the anti-corruption gateway (card [E12] #101) faithfully translates Lumen's authorization API into
/// SISLAB Contracts DTOs: permissions arrive grouped by <c>PermissionGroup</c> with the orphan flag intact,
/// the <c>selected</c> flag reflects a profile's granted permissions, and write use cases forward to the
/// correct Lumen commands (with the company id as <c>ScopeId</c>).
/// </summary>
public sealed class LumenAuthorizationGatewayTests
{
    private static readonly Guid InventoryGroupId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PermCreate = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid PermList = new("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid PermOrphan = new("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly Guid ProfileId = new("bbbbbbbb-0000-0000-0000-000000000001");

    private static IReadOnlyList<ListPermissionsGroupResult> SampleGroups() =>
    [
        new ListPermissionsGroupResult(
            InventoryGroupId,
            "Inventory",
            [
                new ListPermissionsPermissionResult(PermCreate, "Items.Create", "Create item", IsOrphan: false),
                new ListPermissionsPermissionResult(PermList, "Items.List", "List items", IsOrphan: false),
            ]),
        new ListPermissionsGroupResult(
            GroupId: null,
            "Ungrouped",
            [
                new ListPermissionsPermissionResult(PermOrphan, "Legacy.Gone", "Legacy action", IsOrphan: true),
            ]),
    ];

    [Fact]
    public async Task GetPermissionsGrouped_WithoutProfile_KeepsGroupingAndSelectsNothing()
    {
        var mediator = new FakeLumenMediator()
            .On<ListPermissionsQuery>(_ => SampleGroups());
        var gateway = new LumenAuthorizationGateway(mediator);

        IReadOnlyList<PermissionGroupDto> groups =
            await gateway.GetPermissionsGroupedAsync(selectedProfileId: null);

        Assert.Equal(2, groups.Count);

        PermissionGroupDto inventory = groups.Single(g => g.GroupId == InventoryGroupId);
        Assert.Equal("Inventory", inventory.GroupName);
        Assert.Equal(2, inventory.Permissions.Count);
        Assert.All(inventory.Permissions, p => Assert.False(p.Selected));

        PermissionGroupDto ungrouped = groups.Single(g => g.GroupId is null);
        Assert.True(ungrouped.Permissions.Single().IsOrphan);
        Assert.False(ungrouped.Permissions.Single().Selected);
    }

    [Fact]
    public async Task GetPermissionsGrouped_WithProfile_MarksGrantedPermissionsSelected()
    {
        var mediator = new FakeLumenMediator()
            .On<ListPermissionsQuery>(_ => SampleGroups())
            .On<GetProfileQuery>(_ => new GetProfileResult(
                ProfileId, "Coordinator", "Lab coordinator", IsSystem: false, PermissionIds: [PermCreate]));
        var gateway = new LumenAuthorizationGateway(mediator);

        IReadOnlyList<PermissionGroupDto> groups =
            await gateway.GetPermissionsGroupedAsync(ProfileId);

        var flat = groups.SelectMany(g => g.Permissions).ToDictionary(p => p.Id);
        Assert.True(flat[PermCreate].Selected);
        Assert.False(flat[PermList].Selected);
        Assert.False(flat[PermOrphan].Selected);
    }

    [Fact]
    public async Task CreateProfile_ForwardsToLumenAndReturnsNewId()
    {
        Guid newId = Guid.NewGuid();
        var mediator = new FakeLumenMediator()
            .On<CreateProfileCommand>(cmd => new CreateProfileResult(newId, cmd.Name, cmd.Description));
        var gateway = new LumenAuthorizationGateway(mediator);

        Guid result = await gateway.CreateProfileAsync("Coordinator", "Lab coordinator");

        Assert.Equal(newId, result);
        var sent = Assert.IsType<CreateProfileCommand>(mediator.SentRequests.Single());
        Assert.Equal("Coordinator", sent.Name);
    }

    [Fact]
    public async Task SetProfilePermissions_ForwardsIdsAndActorToLumen()
    {
        var mediator = new FakeLumenMediator();
        var gateway = new LumenAuthorizationGateway(mediator);
        Guid[] ids = [PermCreate, PermList];

        await gateway.SetProfilePermissionsAsync(ProfileId, ids, actorUsername: "coordinator@lab");

        var sent = Assert.IsType<SetProfilePermissionsCommand>(mediator.SentRequests.Single());
        Assert.Equal(ProfileId, sent.ProfileId);
        Assert.Equal(ids, sent.PermissionIds);
        Assert.Equal("coordinator@lab", sent.ActorUsername);
    }

    [Fact]
    public async Task AssignProfile_PassesCompanyIdAsScope()
    {
        Guid userId = Guid.NewGuid();
        Guid companyId = Guid.NewGuid();
        var mediator = new FakeLumenMediator();
        var gateway = new LumenAuthorizationGateway(mediator);

        await gateway.AssignProfileAsync(userId, ProfileId, companyId);

        var sent = Assert.IsType<AssignUserProfileCommand>(mediator.SentRequests.Single());
        Assert.Equal(userId, sent.UserId);
        Assert.Equal(ProfileId, sent.ProfileId);
        Assert.Equal(companyId, sent.ScopeId);
    }

    [Fact]
    public async Task RemoveProfile_PassesCompanyIdAsScope()
    {
        Guid userId = Guid.NewGuid();
        Guid companyId = Guid.NewGuid();
        var mediator = new FakeLumenMediator();
        var gateway = new LumenAuthorizationGateway(mediator);

        await gateway.RemoveProfileAsync(userId, ProfileId, companyId);

        var sent = Assert.IsType<RemoveUserProfileCommand>(mediator.SentRequests.Single());
        Assert.Equal(companyId, sent.ScopeId);
    }
}
