using FluentValidation;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Identity.Contracts.Administration;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Experiments.Commands;

/// <summary>
/// Designates a user as responsible for a specific step of an experiment (card [E11]) — step-scoped edit
/// authority. A step may have one or more responsibles; assigning the same user twice is idempotent. The user is
/// referenced by their Lumen user id and must be an active member of the current company (validated through the
/// Identity Contracts port). Managing responsibles is gated at the endpoint by Lumen's <c>[RequirePermission]</c>.
/// </summary>
public sealed record AssignStepResponsibleCommand(
    Guid ExperimentId,
    Guid StepId,
    Guid ResponsibleUserId) : ICommand;

internal sealed class AssignStepResponsibleCommandValidator
    : AbstractValidator<AssignStepResponsibleCommand>
{
    public AssignStepResponsibleCommandValidator()
    {
        RuleFor(command => command.ExperimentId).NotEmpty();
        RuleFor(command => command.StepId).NotEmpty();
        RuleFor(command => command.ResponsibleUserId).NotEmpty();
    }
}

internal sealed class AssignStepResponsibleCommandHandler
    : ICommandHandler<AssignStepResponsibleCommand>
{
    private readonly IExperimentRepository _experiments;
    private readonly ICompanyMembershipQuery _membership;
    private readonly ITenantContext _tenantContext;

    public AssignStepResponsibleCommandHandler(
        IExperimentRepository experiments,
        ICompanyMembershipQuery membership,
        ITenantContext tenantContext)
    {
        _experiments = experiments;
        _membership = membership;
        _tenantContext = tenantContext;
    }

    public async Task<Unit> HandleAsync(
        AssignStepResponsibleCommand request,
        CancellationToken cancellationToken = default)
    {
        Experiment experiment =
            await _experiments.FindByIdAsync(request.ExperimentId, cancellationToken)
            ?? throw new NotFoundException($"Experiment '{request.ExperimentId}' was not found.");

        bool isMember = await _membership.IsActiveMemberAsync(
            _tenantContext.CompanyId, request.ResponsibleUserId, cancellationToken);

        if (!isMember)
            throw new BusinessException(
                $"User '{request.ResponsibleUserId}' is not an active member of the company and cannot be a responsible.");

        // Mutation through the aggregate root only (steps are child entities): the aggregate guards that the step
        // belongs to the experiment.
        experiment.AssignStepResponsible(request.StepId, request.ResponsibleUserId);

        await _experiments.UpdateAsync(experiment, cancellationToken);

        return Unit.Value;
    }
}
