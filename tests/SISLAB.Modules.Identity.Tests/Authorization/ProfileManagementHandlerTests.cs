using SISLAB.Modules.Identity.Application.Authorization;

namespace SISLAB.Modules.Identity.Tests.Authorization;

/// <summary>
/// Proves the profile-management command handlers behind <c>ProfilesController</c> (card [E12] #103): create,
/// update and the idempotent permission-set reconciliation. The handlers are thin orchestrators over
/// <c>ILumenAuthorizationGateway</c> — the tests assert they forward the right arguments (name/description,
/// the exact permission set, the audit actor) and surface the gateway's result, without ever offering a path
/// to create a permission.
/// </summary>
public sealed class ProfileManagementHandlerTests
{
    private static readonly Guid ProfileId = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid PermA = new("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid PermB = new("dddddddd-0000-0000-0000-000000000002");

    [Fact]
    public async Task CreateProfile_ForwardsNameDescriptionAndReturnsNewId()
    {
        Guid expectedId = Guid.NewGuid();
        var gateway = new FakeLumenAuthorizationGateway { CreatedProfileId = expectedId };
        var handler = new CreateProfileCommandHandler(gateway);

        Guid id = await handler.HandleAsync(new CreateProfileCommand("Coordinator", "Lab coordinator"));

        Assert.Equal(expectedId, id);
        Assert.Equal(("Coordinator", "Lab coordinator"), gateway.CreatedProfile);
    }

    [Fact]
    public async Task UpdateProfile_ForwardsIdentityToGateway()
    {
        var gateway = new FakeLumenAuthorizationGateway();
        var handler = new UpdateProfileCommandHandler(gateway);

        await handler.HandleAsync(new UpdateProfileCommand(ProfileId, "Senior analyst", "Updated"));

        Assert.Equal((ProfileId, "Senior analyst", "Updated"), gateway.UpdatedProfile);
    }

    [Fact]
    public async Task SetProfilePermissions_ForwardsExactSetAndActor()
    {
        var gateway = new FakeLumenAuthorizationGateway();
        var handler = new SetProfilePermissionsCommandHandler(gateway);
        Guid[] ids = [PermA, PermB];

        await handler.HandleAsync(new SetProfilePermissionsCommand(ProfileId, ids, "coordinator@lab"));

        Assert.NotNull(gateway.SetPermissionsCall);
        Assert.Equal(ProfileId, gateway.SetPermissionsCall!.Value.ProfileId);
        Assert.Equal(ids, gateway.SetPermissionsCall.Value.PermissionIds);
        Assert.Equal("coordinator@lab", gateway.SetPermissionsCall.Value.Actor);
    }

    [Fact]
    public async Task SetProfilePermissions_WithEmptySet_ClearsAllPermissions()
    {
        var gateway = new FakeLumenAuthorizationGateway();
        var handler = new SetProfilePermissionsCommandHandler(gateway);

        await handler.HandleAsync(new SetProfilePermissionsCommand(ProfileId, [], ActorUsername: null));

        Assert.NotNull(gateway.SetPermissionsCall);
        Assert.Empty(gateway.SetPermissionsCall!.Value.PermissionIds);
    }
}
