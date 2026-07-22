using FluentValidation;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Experiments.Commands;

/// <summary>
/// Removes a user from a step's responsibles (card [E11]). Idempotent — removing someone who is not a responsible
/// is a no-op. Gated at the endpoint by Lumen's <c>[RequirePermission]</c>. No membership check is needed to
/// remove (only to add).
/// </summary>
public sealed record RemoveStepResponsibleCommand(
    Guid ExperimentId,
    Guid StepId,
    Guid ResponsibleUserId) : ICommand;

internal sealed class RemoveStepResponsibleCommandValidator
    : AbstractValidator<RemoveStepResponsibleCommand>
{
    public RemoveStepResponsibleCommandValidator()
    {
        RuleFor(command => command.ExperimentId).NotEmpty();
        RuleFor(command => command.StepId).NotEmpty();
        RuleFor(command => command.ResponsibleUserId).NotEmpty();
    }
}

internal sealed class RemoveStepResponsibleCommandHandler
    : ICommandHandler<RemoveStepResponsibleCommand>
{
    private readonly IExperimentRepository _experiments;

    public RemoveStepResponsibleCommandHandler(IExperimentRepository experiments)
        => _experiments = experiments;

    public async Task<Unit> HandleAsync(
        RemoveStepResponsibleCommand request,
        CancellationToken cancellationToken = default)
    {
        Experiment experiment =
            await _experiments.FindByIdAsync(request.ExperimentId, cancellationToken)
            ?? throw new NotFoundException($"Experiment '{request.ExperimentId}' was not found.");

        experiment.RemoveStepResponsible(request.StepId, request.ResponsibleUserId);

        await _experiments.UpdateAsync(experiment, cancellationToken);

        return Unit.Value;
    }
}
