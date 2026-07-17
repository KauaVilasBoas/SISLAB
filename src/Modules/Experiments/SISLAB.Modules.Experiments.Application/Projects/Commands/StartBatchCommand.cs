using FluentValidation;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Projects.Commands;

/// <summary>
/// Starts a batch: freezes its design (decision F1 — the batch becomes a stable, reproducible cohort) and
/// activates the project (card [E11] #73). Requires at least one group with at least one animal — enforced by the
/// aggregate.
/// </summary>
public sealed record StartBatchCommand(Guid ProjectId, Guid BatchId) : ICommand;

internal sealed class StartBatchCommandValidator : AbstractValidator<StartBatchCommand>
{
    public StartBatchCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.BatchId).NotEmpty();
    }
}

internal sealed class StartBatchCommandHandler : ICommandHandler<StartBatchCommand>
{
    private readonly IProjectRepository _projects;

    public StartBatchCommandHandler(IProjectRepository projects) => _projects = projects;

    public async Task<Unit> HandleAsync(StartBatchCommand request, CancellationToken cancellationToken = default)
    {
        Project project = await _projects.FindByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Project '{request.ProjectId}' was not found.");

        project.StartBatch(request.BatchId);

        await _projects.UpdateAsync(project, cancellationToken);

        return Unit.Value;
    }
}
