using System.Reflection;
using SISLAB.Modules.Identity.Application.Authorization;
using SISLAB.Modules.Identity.Contracts.Authorization;

namespace SISLAB.Modules.Identity.Tests.Authorization;

/// <summary>
/// Guards the explicit product rule (card [E12] #103): the operator creates <b>profiles</b>, but <b>never</b>
/// permissions — permissions are auto-discovered from <c>&lt;Controller&gt;.&lt;Action&gt;</c> and are
/// read-only. These reflection tests fail the build if a permission-creation capability ever sneaks into the
/// authorization surface, whether as a gateway method or a CQRS request.
/// </summary>
public sealed class NoPermissionCreationPathTests
{
    [Fact]
    public void Gateway_ExposesNoPermissionMutationCapability()
    {
        IEnumerable<string> mutatingPermissionMembers = typeof(ILumenAuthorizationGateway)
            .GetMethods()
            .Select(method => method.Name)
            .Where(IsPermissionMutation);

        Assert.Empty(mutatingPermissionMembers);
    }

    [Fact]
    public void ApplicationAssembly_HasNoPermissionCreationRequest()
    {
        Assembly applicationAssembly = typeof(CreateProfileCommand).Assembly;

        IEnumerable<string> offending = applicationAssembly
            .GetTypes()
            .Select(type => type.Name)
            .Where(name =>
                (name.EndsWith("Command", StringComparison.Ordinal) ||
                 name.EndsWith("CommandHandler", StringComparison.Ordinal)) &&
                IsPermissionMutation(name));

        Assert.Empty(offending);
    }

    /// <summary>True when a member/type name reads as "create/add/edit/delete a Permission".</summary>
    private static bool IsPermissionMutation(string name) =>
        name.Contains("Permission", StringComparison.Ordinal) &&
        (name.StartsWith("Create", StringComparison.Ordinal) ||
         name.StartsWith("Add", StringComparison.Ordinal) ||
         name.StartsWith("Edit", StringComparison.Ordinal) ||
         name.StartsWith("Delete", StringComparison.Ordinal));
}
