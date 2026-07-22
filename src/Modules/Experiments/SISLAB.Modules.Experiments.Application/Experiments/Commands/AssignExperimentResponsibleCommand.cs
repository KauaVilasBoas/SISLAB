using FluentValidation;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Identity.Contracts.Administration;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Experiments.Commands;

/// <summary>
/// Sets (or replaces) the experiment's lead responsible (card [E11]) — the "bigger chain" with full edit
/// authority over the experiment. The responsible is referenced by their Lumen user id and must be an active
/// member of the current company; membership is validated through the Identity module's Contracts port, never a
/// cross-module query.
/// </summary>
/// <remarks>
/// Managing responsibles is a coordination action, gated at the endpoint by Lumen's <c>[RequirePermission]</c>
/// (the coordinator permission — DP-3). Reaching this handler therefore already implies the caller may manage the
/// experiment's responsibles, so the handler validates the <i>target</i> (membership) rather than re-deriving the
/// caller's authority. This is complementary to — never a replacement for — the endpoint permission gate.
/// </remarks>
public sealed record AssignExperimentResponsibleCommand(Guid ExperimentId, Guid ResponsibleUserId) : ICommand;

internal sealed class AssignExperimentResponsibleCommandValidator
    : AbstractValidator<AssignExperimentResponsibleCommand>
{
    public AssignExperimentResponsibleCommandValidator()
    {
        RuleFor(command => command.ExperimentId).NotEmpty();
        RuleFor(command => command.ResponsibleUserId).NotEmpty();
    }
}

internal sealed class AssignExperimentResponsibleCommandHandler
    : ICommandHandler<AssignExperimentResponsibleCommand>
{
    private readonly IExperimentRepository _experiments;
    private readonly ICompanyMembershipQuery _membership;
    private readonly ITenantContext _tenantContext;

    public AssignExperimentResponsibleCommandHandler(
        IExperimentRepository experiments,
        ICompanyMembershipQuery membership,
        ITenantContext tenantContext)
    {
        _experiments = experiments;
        _membership = membership;
        _tenantContext = tenantContext;
    }

    public async Task<Unit> HandleAsync(
        AssignExperimentResponsibleCommand request,
        CancellationToken cancellationToken = default)
    {
        Experiment experiment =
            await _experiments.FindByIdAsync(request.ExperimentId, cancellationToken)
            ?? throw new NotFoundException($"Experiment '{request.ExperimentId}' was not found.");

        await EnsureIsCompanyMemberAsync(request.ResponsibleUserId, cancellationToken);

        experiment.AssignResponsible(request.ResponsibleUserId);

        await _experiments.UpdateAsync(experiment, cancellationToken);

        return Unit.Value;
    }

    private async Task EnsureIsCompanyMemberAsync(Guid userId, CancellationToken cancellationToken)
    {
        bool isMember = await _membership.IsActiveMemberAsync(
            _tenantContext.CompanyId, userId, cancellationToken);

        if (!isMember)
            throw new BusinessException(
                $"User '{userId}' is not an active member of the company and cannot be a responsible.");
    }
}
