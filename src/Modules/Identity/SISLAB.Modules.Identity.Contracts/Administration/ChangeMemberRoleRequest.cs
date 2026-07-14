using SISLAB.SharedKernel.Authorization;

namespace SISLAB.Modules.Identity.Contracts.Administration;

/// <summary>
/// Request body for changing a member's business <see cref="Role"/> in the active company
/// (card [E12] #77e). The target user comes from the route and the company from the httpOnly cookie —
/// never from this body — so the payload carries only the new role.
/// </summary>
/// <param name="Role">The role to assign to the member.</param>
public sealed record ChangeMemberRoleRequest(Role Role);
