using FluentValidation;
using SISLAB.Modules.Identity.Contracts.Authorization;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Authorization;

/// <summary>
/// Idempotently reconciles a profile's permissions to exactly the checked set (card [E12] #103): this is what
/// "save" does after the operator ticks/unticks the checkboxes. Permissions in <see cref="PermissionIds"/> not
/// yet granted are added, permissions granted but no longer checked are removed, and re-sending the same set is
/// a no-op — so the command is safe to retry and models the whole "set of permissions" as one atomic edit.
///
/// <para>The operator sets a profile's permissions but never creates a <c>Permission</c>: the ids must come
/// from the auto-discovered catalogue (Lumen rejects unknown ids). Overwriting a system profile's permissions
/// is refused by Lumen. <see cref="ActorUsername"/> is the audit label for who made the change; it is supplied
/// by the controller from the authenticated principal, never from the request body.</para>
/// </summary>
public sealed record SetProfilePermissionsCommand(
    Guid ProfileId,
    IReadOnlyList<Guid> PermissionIds,
    string? ActorUsername) : ICommand;

internal sealed class SetProfilePermissionsCommandValidator : AbstractValidator<SetProfilePermissionsCommand>
{
    public SetProfilePermissionsCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.PermissionIds).NotNull();
        RuleForEach(command => command.PermissionIds).NotEmpty();
    }
}

internal sealed class SetProfilePermissionsCommandHandler : ICommandHandler<SetProfilePermissionsCommand>
{
    private readonly ILumenAuthorizationGateway _authorization;

    public SetProfilePermissionsCommandHandler(ILumenAuthorizationGateway authorization)
        => _authorization = authorization;

    public async Task<Unit> HandleAsync(
        SetProfilePermissionsCommand request,
        CancellationToken cancellationToken = default)
    {
        await _authorization.SetProfilePermissionsAsync(
            request.ProfileId, request.PermissionIds, request.ActorUsername, cancellationToken);

        return Unit.Value;
    }
}
