namespace SISLAB.Api.Tests.Authorization;

/// <summary>
/// Pins the HTTP wiring of the three write endpoints closed in this slice (SISLAB-01/05/10): each must be discovered
/// as a permission-gated write with the exact <c>{Controller}.{Action}</c> code the frontend catalogue and the seed
/// migrations reference. This guards the wiring itself — a renamed action, a dropped <c>[RequirePermission]</c> or a
/// changed verb would move the code and silently break the contract (403 for everyone, or an ungated write). It uses
/// the same reflection discovery (<see cref="ControllerActionCatalog"/>) the drift and write-permission tests use.
/// </summary>
public sealed class PreparationSchedulingEndpointWiringTests
{
    [Theory]
    [InlineData("ProjectsController", "PrepareGroupSolution", "Projects.PrepareGroupSolution")]   // SISLAB-01
    [InlineData("ExperimentsController", "ApplyDilutionScheme", "Experiments.ApplyDilutionScheme")] // SISLAB-05
    [InlineData("SchedulingController", "Generate", "Scheduling.Generate")]                        // SISLAB-10
    public void WriteEndpoint_isGated_withTheExpectedPermissionCode(
        string controllerName, string actionName, string expectedCode)
    {
        ControllerAction? action = ControllerActionCatalog.All.FirstOrDefault(a =>
            a.Controller.Name == controllerName && a.Method.Name == actionName);

        Assert.True(action is not null, $"{controllerName}.{actionName} was not discovered as a controller action.");
        Assert.True(action!.IsWrite, $"{controllerName}.{actionName} must be a write endpoint (POST/PUT/DELETE).");
        Assert.True(action.HasRequirePermission, $"{controllerName}.{actionName} must carry [RequirePermission].");
        Assert.Equal(expectedCode, action.PermissionCode);
    }

    [Theory]
    [InlineData("ExperimentsController", "ComputeDilutionScheme")] // SISLAB-05: stateless compute, a read
    [InlineData("ProjectsController", "ListPreparations")]         // SISLAB-01: read-side listing
    public void ReadEndpoint_isNotPermissionGated(string controllerName, string actionName)
    {
        ControllerAction? action = ControllerActionCatalog.All.FirstOrDefault(a =>
            a.Controller.Name == controllerName && a.Method.Name == actionName);

        Assert.True(action is not null, $"{controllerName}.{actionName} was not discovered as a controller action.");
        Assert.False(action!.IsWrite, $"{controllerName}.{actionName} must be a read endpoint (GET/POST-compute).");
    }
}
