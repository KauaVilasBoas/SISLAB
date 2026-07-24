using FluentValidation;
using SISLAB.Modules.Configuration.Contracts;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Projects.Commands;

/// <summary>
/// Binds a batch ("leva") of a project to an experimental model / induction protocol (SISLAB-04). The model is
/// owned by the Configuration bounded context and referenced here <b>by value</b> (its id) — the batch stores only
/// the <c>ExperimentalModelId</c>, never a cross-module navigation. Binding is allowed only while the batch's design
/// is open (planned); a started batch is frozen, so its model version is stable and reproducible.
/// </summary>
/// <remarks>
/// The model's existence and tenant ownership are validated through the Configuration module's public
/// <see cref="ILabConfiguration"/> port before mutating the aggregate — a cross-module check that crosses the
/// boundary only via Contracts (module isolation, section 2), never a direct query into Configuration's internals.
/// The active company is implicit on that port (resolved from <c>ITenantContext</c> inside Configuration), so a
/// caller can never bind to another tenant's model.
/// </remarks>
public sealed record BindBatchToModelCommand(Guid ProjectId, Guid BatchId, Guid ExperimentalModelId) : ICommand;

internal sealed class BindBatchToModelCommandValidator : AbstractValidator<BindBatchToModelCommand>
{
    public BindBatchToModelCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.BatchId).NotEmpty();
        RuleFor(command => command.ExperimentalModelId).NotEmpty();
    }
}

internal sealed class BindBatchToModelCommandHandler : ICommandHandler<BindBatchToModelCommand>
{
    private readonly IProjectRepository _projects;
    private readonly ILabConfiguration _labConfiguration;

    public BindBatchToModelCommandHandler(IProjectRepository projects, ILabConfiguration labConfiguration)
    {
        _projects = projects;
        _labConfiguration = labConfiguration;
    }

    public async Task<Unit> HandleAsync(BindBatchToModelCommand request, CancellationToken cancellationToken = default)
    {
        Project project = await _projects.FindByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Project '{request.ProjectId}' was not found.");

        bool modelExists =
            await _labConfiguration.ExperimentalModelExistsAsync(request.ExperimentalModelId, cancellationToken);

        if (!modelExists)
            throw new BusinessException(
                $"Experimental model '{request.ExperimentalModelId}' was not found for the active company.");

        project.BindBatchToModel(request.BatchId, request.ExperimentalModelId);

        await _projects.UpdateAsync(project, cancellationToken);

        return Unit.Value;
    }
}
