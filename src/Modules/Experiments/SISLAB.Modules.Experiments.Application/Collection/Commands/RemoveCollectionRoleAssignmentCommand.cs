using FluentValidation;
using SISLAB.Modules.Experiments.Domain.Collection;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Collection.Commands;

/// <summary>
/// Removes a role assignment from a collection plan (SISLAB-08). It is an error to remove a role the plan has not
/// assigned.
/// </summary>
public sealed record RemoveCollectionRoleAssignmentCommand(Guid PlanId, Guid RoleId) : ICommand;

internal sealed class RemoveCollectionRoleAssignmentCommandValidator
    : AbstractValidator<RemoveCollectionRoleAssignmentCommand>
{
    public RemoveCollectionRoleAssignmentCommandValidator()
    {
        RuleFor(command => command.PlanId).NotEmpty();
        RuleFor(command => command.RoleId).NotEmpty();
    }
}

internal sealed class RemoveCollectionRoleAssignmentCommandHandler
    : ICommandHandler<RemoveCollectionRoleAssignmentCommand>
{
    private readonly ICollectionPlanRepository _plans;

    public RemoveCollectionRoleAssignmentCommandHandler(ICollectionPlanRepository plans) => _plans = plans;

    public async Task<Unit> HandleAsync(
        RemoveCollectionRoleAssignmentCommand request,
        CancellationToken cancellationToken = default)
    {
        CollectionPlan plan = await _plans.FindByIdAsync(request.PlanId, cancellationToken)
            ?? throw new NotFoundException($"Collection plan '{request.PlanId}' was not found.");

        plan.RemoveAssignment(request.RoleId);

        await _plans.UpdateAsync(plan, cancellationToken);

        return Unit.Value;
    }
}
