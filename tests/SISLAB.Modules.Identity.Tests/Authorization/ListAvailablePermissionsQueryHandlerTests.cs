using SISLAB.Modules.Identity.Application.Authorization;
using SISLAB.Modules.Identity.Contracts.Authorization;

namespace SISLAB.Modules.Identity.Tests.Authorization;

/// <summary>
/// Proves the read handler behind <c>ProfilesController.ListAvailablePermissions</c> (card [E12] #102): it
/// forwards the optional profile id to the gateway and returns the grouped catalogue unchanged. Grouping,
/// the <c>selected</c> flag and orphan handling are the gateway's responsibility (covered by
/// <see cref="LumenAuthorizationGatewayTests"/>); here we assert the handler is a faithful, side-effect-free
/// pass-through — no permission is ever created.
/// </summary>
public sealed class ListAvailablePermissionsQueryHandlerTests
{
    private static readonly Guid ProfileId = new("bbbbbbbb-0000-0000-0000-000000000009");

    private static readonly IReadOnlyList<PermissionGroupDto> SampleGroups =
    [
        new PermissionGroupDto(
            Guid.NewGuid(),
            "Inventory",
            [new PermissionOptionDto(Guid.NewGuid(), "Items.Create", "Create item", IsOrphan: false, Selected: true)]),
    ];

    [Fact]
    public async Task HandleAsync_WithProfileId_PassesItThroughAndReturnsGroups()
    {
        var gateway = new FakeLumenAuthorizationGateway { GroupsToReturn = SampleGroups };
        var handler = new ListAvailablePermissionsQueryHandler(gateway);

        ListAvailablePermissionsResult result =
            await handler.HandleAsync(new ListAvailablePermissionsQuery(ProfileId));

        Assert.Equal(ProfileId, gateway.LastSelectedProfileId);
        Assert.Same(SampleGroups, result.Groups);
    }

    [Fact]
    public async Task HandleAsync_WithoutProfileId_PassesNullSelection()
    {
        var gateway = new FakeLumenAuthorizationGateway { GroupsToReturn = SampleGroups };
        var handler = new ListAvailablePermissionsQueryHandler(gateway);

        await handler.HandleAsync(new ListAvailablePermissionsQuery(ProfileId: null));

        Assert.Null(gateway.LastSelectedProfileId);
    }
}
