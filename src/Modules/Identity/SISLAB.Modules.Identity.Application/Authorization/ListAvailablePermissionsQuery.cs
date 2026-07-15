using SISLAB.Modules.Identity.Contracts.Authorization;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Authorization;

/// <summary>
/// Lists every permission grouped by its Lumen <c>PermissionGroup</c> (card [E12] #102), so the
/// profile-management screen can render one checkbox per permission under its group heading.
///
/// <para>When <see cref="ProfileId"/> is supplied (editing an existing profile), each permission already
/// granted to that profile is returned with <see cref="PermissionOptionDto.Selected"/> set — pre-ticking the
/// boxes. When it is null (creating a new profile) nothing is selected.</para>
///
/// <para>Permissions are global (not tenant-scoped) and read-only: this query never creates or edits a
/// permission, it only projects the catalogue seeded by <c>SISLAB.Migrations</c> into <c>Lumen.Permission</c>
/// (keyed by the <c>&lt;Controller&gt;.&lt;Action&gt;</c> convention Lumen enforces).</para>
/// </summary>
/// <param name="ProfileId">Optional profile whose granted permissions should be pre-selected.</param>
public sealed record ListAvailablePermissionsQuery(Guid? ProfileId)
    : IQuery<ListAvailablePermissionsResult>;

/// <param name="Groups">Permissions grouped by <c>PermissionGroup</c>; never null, ordered by Lumen.</param>
public sealed record ListAvailablePermissionsResult(IReadOnlyList<PermissionGroupDto> Groups);

internal sealed class ListAvailablePermissionsQueryHandler
    : IQueryHandler<ListAvailablePermissionsQuery, ListAvailablePermissionsResult>
{
    private readonly ILumenAuthorizationGateway _authorization;

    public ListAvailablePermissionsQueryHandler(ILumenAuthorizationGateway authorization)
        => _authorization = authorization;

    public async Task<ListAvailablePermissionsResult> HandleAsync(
        ListAvailablePermissionsQuery request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PermissionGroupDto> groups =
            await _authorization.GetPermissionsGroupedAsync(request.ProfileId, cancellationToken);

        return new ListAvailablePermissionsResult(groups);
    }
}
