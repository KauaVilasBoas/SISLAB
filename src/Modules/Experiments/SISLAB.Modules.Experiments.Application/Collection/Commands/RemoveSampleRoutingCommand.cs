using FluentValidation;
using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.Modules.Experiments.Domain.Collection;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Collection.Commands;

/// <summary>
/// Removes one matrix row (a sample type's routing) from a collection plan (SISLAB-08). It is an error to remove a
/// sample type the matrix does not route.
/// </summary>
public sealed record RemoveSampleRoutingCommand(Guid PlanId, SampleType SampleType) : ICommand;

internal sealed class RemoveSampleRoutingCommandValidator : AbstractValidator<RemoveSampleRoutingCommand>
{
    public RemoveSampleRoutingCommandValidator()
    {
        RuleFor(command => command.PlanId).NotEmpty();
        RuleFor(command => command.SampleType).IsInEnum();
    }
}

internal sealed class RemoveSampleRoutingCommandHandler : ICommandHandler<RemoveSampleRoutingCommand>
{
    private readonly ICollectionPlanRepository _plans;

    public RemoveSampleRoutingCommandHandler(ICollectionPlanRepository plans) => _plans = plans;

    public async Task<Unit> HandleAsync(RemoveSampleRoutingCommand request, CancellationToken cancellationToken = default)
    {
        CollectionPlan plan = await _plans.FindByIdAsync(request.PlanId, cancellationToken)
            ?? throw new NotFoundException($"Collection plan '{request.PlanId}' was not found.");

        plan.RemoveRouting(request.SampleType);

        await _plans.UpdateAsync(plan, cancellationToken);

        return Unit.Value;
    }
}
