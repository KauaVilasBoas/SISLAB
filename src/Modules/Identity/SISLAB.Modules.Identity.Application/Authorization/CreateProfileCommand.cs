using FluentValidation;
using SISLAB.Modules.Identity.Contracts.Authorization;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Authorization;

/// <summary>
/// Creates a new authorization profile (card [E12] #103): a named, reusable set of permissions the operator
/// can later assign to members. The command carries only the profile's identity (name + description); its
/// permissions are set separately through <see cref="SetProfilePermissionsCommand"/> so the two concerns —
/// "what the profile is" and "what it can do" — stay independently editable.
///
/// <para>Profiles are global to the Lumen instance, not tenant-scoped: creation does not read the active
/// company. Uniqueness of the name among active profiles is enforced by Lumen; a clash surfaces as a conflict.
/// This never creates a <c>Permission</c> — permissions are auto-discovered and read-only.</para>
/// </summary>
public sealed record CreateProfileCommand(string Name, string Description) : ICommand<Guid>;

internal sealed class CreateProfileCommandValidator : AbstractValidator<CreateProfileCommand>
{
    public CreateProfileCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(500);
    }
}

internal sealed class CreateProfileCommandHandler : ICommandHandler<CreateProfileCommand, Guid>
{
    private readonly ILumenAuthorizationGateway _authorization;

    public CreateProfileCommandHandler(ILumenAuthorizationGateway authorization)
        => _authorization = authorization;

    public Task<Guid> HandleAsync(CreateProfileCommand request, CancellationToken cancellationToken = default)
        => _authorization.CreateProfileAsync(request.Name, request.Description, cancellationToken);
}
