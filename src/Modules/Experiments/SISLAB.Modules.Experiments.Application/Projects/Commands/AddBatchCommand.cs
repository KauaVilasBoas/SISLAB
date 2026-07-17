using FluentValidation;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Projects.Commands;

/// <summary>
/// Adds a new batch ("leva") to a project (card [E11] #73, decision F1 — the batch fixes the design version).
/// The new batch is pinned to the project's current design version and starts Planned. Returns the new batch id.
/// </summary>
public sealed record AddBatchCommand(Guid ProjectId, string Name) : ICommand<Guid>;

internal sealed class AddBatchCommandValidator : AbstractValidator<AddBatchCommand>
{
    public AddBatchCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
    }
}

internal sealed class AddBatchCommandHandler : ICommandHandler<AddBatchCommand, Guid>
{
    private readonly IProjectRepository _projects;

    public AddBatchCommandHandler(IProjectRepository projects) => _projects = projects;

    public async Task<Guid> HandleAsync(AddBatchCommand request, CancellationToken cancellationToken = default)
    {
        Project project = await _projects.FindByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Project '{request.ProjectId}' was not found.");

        Batch batch = project.AddBatch(request.Name);

        await _projects.UpdateAsync(project, cancellationToken);

        return batch.Id;
    }
}
