using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Modules.Identity.Application.Administration;
using SISLAB.Modules.Identity.Contracts.Administration;

namespace SISLAB.Api.Tests.Authorization;

/// <summary>
/// Contract guard for the member role-management endpoint (card [E12] #77e):
/// <c>PUT api/admin/companies/active/members/{userId}/role</c> on <c>CompanyMembersController</c>.
///
/// <para>Pins the HTTP contract the SPA (E7) codes against — verb, route, request body and the declared
/// status codes — plus the permission gate, as a reflection test that does not need a live database or an
/// authenticated HTTP round-trip. The end-to-end authorization behaviour (Coordinator 200 / others 403) is
/// covered by the enforcement tests; the write-permission decoration is also cross-checked by
/// <see cref="WriteEndpointPermissionTests"/> via the shared <see cref="ControllerActionCatalog"/>.</para>
/// </summary>
public sealed class ChangeMemberRoleEndpointContractTests
{
    private static readonly MethodInfo Action = typeof(CompanyMembersController)
        .GetMethod(nameof(CompanyMembersController.ChangeMemberRole))!;

    [Fact]
    public void Action_Exists_OnCompanyMembersController()
    {
        Assert.NotNull(Action);
    }

    [Fact]
    public void Action_IsHttpPut_OnUserScopedRoleRoute()
    {
        HttpPutAttribute? put = Action.GetCustomAttribute<HttpPutAttribute>();

        Assert.NotNull(put);
        Assert.Equal("{userId:guid}/role", put!.Template);
    }

    [Fact]
    public void Action_IsGatedBy_RequirePermission()
    {
        bool hasRequirePermission = Action.GetCustomAttributes()
            .Any(a => a.GetType().Name == "RequirePermissionAttribute");

        Assert.True(hasRequirePermission,
            "ChangeMemberRole is a write/management action and must carry [RequirePermission].");
    }

    [Fact]
    public void Action_TakesRole_FromBody_And_UserId_FromRoute()
    {
        ParameterInfo[] parameters = Action.GetParameters();

        // userId comes from the route (a Guid), the role from the [FromBody] request DTO — the company
        // is never a parameter: it is resolved from the httpOnly cookie inside the action.
        Assert.Contains(parameters, p => p.ParameterType == typeof(Guid) && p.Name == "userId");

        ParameterInfo body = Assert.Single(
            parameters.Where(p => p.ParameterType == typeof(ChangeMemberRoleRequest)));
        Assert.NotNull(body.GetCustomAttribute<FromBodyAttribute>());
    }

    [Fact]
    public void Action_Declares_ManagementStatusCodes()
    {
        int[] declared = Action.GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Select(a => a.StatusCode)
            .ToArray();

        Assert.Contains(200, declared); // success
        Assert.Contains(403, declared); // caller lacks the coordination permission
        Assert.Contains(404, declared); // company (active tenant) not found
        Assert.Contains(422, declared); // domain invariant (e.g. last Coordinator) violated
    }
}
