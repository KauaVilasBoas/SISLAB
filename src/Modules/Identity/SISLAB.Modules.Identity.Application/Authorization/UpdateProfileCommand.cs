using FluentValidation;
using SISLAB.Modules.Identity.Contracts.Authorization;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Authorization;

/// <summary>
/// Renames/re-describes an existing profile (card [E12] #103). Editing a profile's <i>identity</i> is
/// separate from editing its <i>permissions</i> (<see cref="SetProfilePermissionsCommand"/>). Lumen rejects
/// an unknown profile and enforces name uniqueness among active profiles.
/// </summary>
public sealed record UpdateProfileCommand(Guid ProfileId, string Name, string Description) : ICommand;

internal sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(500);
    }
}

internal sealed class UpdateProfileCommandHandler : ICommandHandler<UpdateProfileCommand>
{
    private readonly ILumenAuthorizationGateway _authorization;

    public UpdateProfileCommandHandler(ILumenAuthorizationGateway authorization)
        => _authorization = authorization;

    public async Task<Unit> HandleAsync(UpdateProfileCommand request, CancellationToken cancellationToken = default)
    {
        await _authorization.UpdateProfileAsync(
            request.ProfileId, request.Name, request.Description, cancellationToken);

        return Unit.Value;
    }
}
