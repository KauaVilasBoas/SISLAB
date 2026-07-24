using FluentValidation;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Projects.Commands;

/// <summary>
/// Adds a cage (caixa) to a batch of a project (SISLAB-03) — the physical housing unit animals arrive in before
/// randomization. The optional <paramref name="Capacity"/> (e.g. 4 in the current lab) is a study parameter, never a
/// fixed constant. Allowed only while the batch's design is still open (Planned). Returns the new cage id.
/// </summary>
public sealed record AddCageCommand(
    Guid ProjectId,
    Guid BatchId,
    string Name,
    int? Capacity) : ICommand<Guid>;

internal sealed class AddCageCommandValidator : AbstractValidator<AddCageCommand>
{
    public AddCageCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.BatchId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Capacity).GreaterThanOrEqualTo(1).When(c => c.Capacity.HasValue);
    }
}

internal sealed class AddCageCommandHandler : ICommandHandler<AddCageCommand, Guid>
{
    private readonly IProjectRepository _projects;

    public AddCageCommandHandler(IProjectRepository projects) => _projects = projects;

    public async Task<Guid> HandleAsync(AddCageCommand request, CancellationToken cancellationToken = default)
    {
        Project project = await _projects.FindByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Project '{request.ProjectId}' was not found.");

        Cage cage = project.AddCage(request.BatchId, request.Name, request.Capacity);

        await _projects.UpdateAsync(project, cancellationToken);

        return cage.Id;
    }
}
