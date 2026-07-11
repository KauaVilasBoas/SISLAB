using FluentValidation;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Application.StockMovements;

/// <summary>
/// Registers a consumption of an item, decreasing its balance. The consumption may optionally reference
/// the experiment it was consumed for.
/// </summary>
/// <remarks>
/// <paramref name="ExperimentId"/> is a cross-module reference held <b>by value</b> (Guid), with no FK or
/// navigation to the Experiment module (which is out of the current backlog) — decision recorded on card
/// [E3] #24. It is carried on the command as a structured reference for the consumption report (card #31)
/// and is not folded into the aggregate here. The operator is the authenticated user (audit trail #57),
/// never taken from the payload.
/// </remarks>
public sealed record RegisterConsumptionCommand(
    Guid StockItemId,
    decimal Quantity,
    string Unit,
    Guid? ExperimentId,
    DateOnly? OccurredOn) : ICommand;

internal sealed class RegisterConsumptionCommandValidator : AbstractValidator<RegisterConsumptionCommand>
{
    public RegisterConsumptionCommandValidator(IClock clock)
    {
        RuleFor(command => command.StockItemId).NotEmpty();
        RuleFor(command => command.Quantity).GreaterThan(0m);
        RuleFor(command => command.Unit).NotEmpty();

        RuleFor(command => command.OccurredOn!.Value)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(clock.UtcNow))
            .When(command => command.OccurredOn.HasValue)
            .WithMessage("The operation date cannot be in the future.");
    }
}

internal sealed class RegisterConsumptionCommandHandler : ICommandHandler<RegisterConsumptionCommand>
{
    private readonly IStockItemRepository _stockItems;

    public RegisterConsumptionCommandHandler(IStockItemRepository stockItems)
        => _stockItems = stockItems;

    public async Task<Unit> HandleAsync(
        RegisterConsumptionCommand request,
        CancellationToken cancellationToken = default)
    {
        StockItem item = await _stockItems.FindByIdAsync(request.StockItemId, cancellationToken)
            ?? throw new NotFoundException("StockItem", request.StockItemId);

        Quantity consumed = Quantity.Of(request.Quantity, UnitOfMeasure.FromSymbol(request.Unit));

        item.RegisterConsumption(consumed);

        await _stockItems.UpdateAsync(item, cancellationToken);

        return Unit.Value;
    }
}
