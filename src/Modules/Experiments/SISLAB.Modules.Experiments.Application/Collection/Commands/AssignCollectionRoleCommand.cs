using FluentValidation;
using SISLAB.Modules.Configuration.Contracts;
using SISLAB.Modules.Experiments.Domain.Collection;
using SISLAB.Modules.Identity.Contracts.Administration;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Collection.Commands;

/// <summary>
/// Assigns a company member to a collection role on a plan (SISLAB-08) — e.g. "Anestesia → Daiane". Idempotent by role:
/// assigning an already-assigned role reassigns it to the new member, so a role has exactly one person in charge.
/// </summary>
/// <remarks>
/// Both sides are validated across module boundaries via Contracts (module isolation, section 2): the role must exist in
/// the active company's Configuration role catalogue (<see cref="ILabConfiguration"/>), and the user must be an active
/// member of the company (<see cref="ICompanyMembershipQuery"/>), mirroring how a step responsible is validated. The
/// plan keeps both ids by value.
/// </remarks>
public sealed record AssignCollectionRoleCommand(Guid PlanId, Guid RoleId, Guid UserId) : ICommand;

internal sealed class AssignCollectionRoleCommandValidator : AbstractValidator<AssignCollectionRoleCommand>
{
    public AssignCollectionRoleCommandValidator()
    {
        RuleFor(command => command.PlanId).NotEmpty();
        RuleFor(command => command.RoleId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
    }
}

internal sealed class AssignCollectionRoleCommandHandler : ICommandHandler<AssignCollectionRoleCommand>
{
    private readonly ICollectionPlanRepository _plans;
    private readonly ILabConfiguration _labConfiguration;
    private readonly ICompanyMembershipQuery _membership;
    private readonly ITenantContext _tenantContext;

    public AssignCollectionRoleCommandHandler(
        ICollectionPlanRepository plans,
        ILabConfiguration labConfiguration,
        ICompanyMembershipQuery membership,
        ITenantContext tenantContext)
    {
        _plans = plans;
        _labConfiguration = labConfiguration;
        _membership = membership;
        _tenantContext = tenantContext;
    }

    public async Task<Unit> HandleAsync(AssignCollectionRoleCommand request, CancellationToken cancellationToken = default)
    {
        CollectionPlan plan = await _plans.FindByIdAsync(request.PlanId, cancellationToken)
            ?? throw new NotFoundException($"Collection plan '{request.PlanId}' was not found.");

        if (!await _labConfiguration.CollectionRoleExistsAsync(request.RoleId, cancellationToken))
            throw new BusinessException(
                $"Collection role '{request.RoleId}' does not exist for the active company and cannot be assigned.");

        if (!await _membership.IsActiveMemberAsync(_tenantContext.CompanyId, request.UserId, cancellationToken))
            throw new BusinessException(
                $"User '{request.UserId}' is not an active member of the company and cannot be assigned a role.");

        plan.AssignRole(request.RoleId, request.UserId);

        await _plans.UpdateAsync(plan, cancellationToken);

        return Unit.Value;
    }
}
