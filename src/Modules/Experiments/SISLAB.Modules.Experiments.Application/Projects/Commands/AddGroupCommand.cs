using FluentValidation;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Projects.Commands;

/// <summary>
/// Adds a dose group (treatment arm) to a batch of a project (card [E11] #73). The group carries its
/// <paramref name="DoseAmount"/> and <paramref name="DoseUnit"/> (zero amount models the vehicle/control arm).
/// Allowed only while the batch's design is still open (Planned). Returns the new group id.
/// </summary>
public sealed record AddGroupCommand(
    Guid ProjectId,
    Guid BatchId,
    string Name,
    decimal DoseAmount,
    string DoseUnit) : ICommand<Guid>;

internal sealed class AddGroupCommandValidator : AbstractValidator<AddGroupCommand>
{
    public AddGroupCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.BatchId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
        RuleFor(command => command.DoseAmount).GreaterThanOrEqualTo(0);
        RuleFor(command => command.DoseUnit).NotEmpty().MaximumLength(30);
    }
}

internal sealed class AddGroupCommandHandler : ICommandHandler<AddGroupCommand, Guid>
{
    private readonly IProjectRepository _projects;

    public AddGroupCommandHandler(IProjectRepository projects) => _projects = projects;

    public async Task<Guid> HandleAsync(AddGroupCommand request, CancellationToken cancellationToken = default)
    {
        Project project = await _projects.FindByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Project '{request.ProjectId}' was not found.");

        Group group = project.AddGroup(
            request.BatchId,
            request.Name,
            Dose.Of(request.DoseAmount, request.DoseUnit));

        await _projects.UpdateAsync(project, cancellationToken);

        return group.Id;
    }
}
