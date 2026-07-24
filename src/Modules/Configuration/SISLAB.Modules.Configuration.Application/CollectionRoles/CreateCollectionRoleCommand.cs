using FluentValidation;
using SISLAB.Modules.Configuration.Domain.CollectionRoles;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Application.CollectionRoles;

/// <summary>
/// Creates a per-tenant collection role (SISLAB-08): the configurable job (e.g. "Volante", "Anestesia", "Sangue") a lab
/// cadasters and later assigns to a person on the collection sheet. Write-side: it maps the flat payload onto the
/// aggregate and lets the unit of work commit. Returns the new role id.
/// </summary>
/// <remarks>
/// Nothing lab-specific is fixed — the role name and description are supplied by the caller. A tenant may hold only one
/// role per name; the handler pre-checks that (the DB unique index is the final guard), so re-cadastering the same name
/// is a conflict rather than a silent second row.
/// </remarks>
public sealed record CreateCollectionRoleCommand(string Name, string? Description) : ICommand<Guid>;

internal sealed class CreateCollectionRoleCommandValidator : AbstractValidator<CreateCollectionRoleCommand>
{
    public CreateCollectionRoleCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(80);
        RuleFor(command => command.Description).MaximumLength(500);
    }
}

internal sealed class CreateCollectionRoleCommandHandler : ICommandHandler<CreateCollectionRoleCommand, Guid>
{
    private readonly ICollectionRoleRepository _roles;

    public CreateCollectionRoleCommandHandler(ICollectionRoleRepository roles) => _roles = roles;

    public async Task<Guid> HandleAsync(
        CreateCollectionRoleCommand request,
        CancellationToken cancellationToken = default)
    {
        if (await _roles.NameExistsAsync(request.Name, cancellationToken))
            throw new ConflictException($"A collection role named '{request.Name.Trim()}' already exists.");

        CollectionRole role = CollectionRole.Create(request.Name, request.Description);

        await _roles.AddAsync(role, cancellationToken);

        return role.Id;
    }
}
