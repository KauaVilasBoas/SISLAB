using FluentValidation;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Projects.Commands;

/// <summary>
/// Assigns (or moves) an animal to a treatment group after basal/induction (SISLAB-03) — the randomization step, and
/// the way a discrepant cage is redistributed across groups. The group must belong to the same batch as the animal.
/// Allowed only while the batch's design is still open (Planned): assignment locks once the leva starts.
/// </summary>
public sealed record AssignAnimalToGroupCommand(
    Guid ProjectId,
    Guid BatchId,
    Guid AnimalId,
    Guid GroupId) : ICommand;

internal sealed class AssignAnimalToGroupCommandValidator : AbstractValidator<AssignAnimalToGroupCommand>
{
    public AssignAnimalToGroupCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.BatchId).NotEmpty();
        RuleFor(command => command.AnimalId).NotEmpty();
        RuleFor(command => command.GroupId).NotEmpty();
    }
}

internal sealed class AssignAnimalToGroupCommandHandler : ICommandHandler<AssignAnimalToGroupCommand>
{
    private readonly IProjectRepository _projects;

    public AssignAnimalToGroupCommandHandler(IProjectRepository projects) => _projects = projects;

    public async Task<Unit> HandleAsync(
        AssignAnimalToGroupCommand request,
        CancellationToken cancellationToken = default)
    {
        Project project = await _projects.FindByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Project '{request.ProjectId}' was not found.");

        project.AssignAnimalToGroup(request.BatchId, request.AnimalId, request.GroupId);

        await _projects.UpdateAsync(project, cancellationToken);

        return Unit.Value;
    }
}
