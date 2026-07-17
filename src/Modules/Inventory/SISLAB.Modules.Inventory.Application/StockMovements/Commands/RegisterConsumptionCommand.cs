using FluentValidation;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Inventory.Application.Audit;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Application.StockMovements.Commands;

/// <summary>
/// Registers a consumption of an item, decreasing its balance. The consumption may optionally reference
/// the experiment it was consumed for.
/// </summary>
/// <remarks>
/// <paramref name="ExperimentId"/> is a cross-module reference held <b>by value</b> (Guid), with no FK or
/// navigation to the Experiment module (which is out of the current backlog) — decision recorded on card
/// [E3] #24. It is not folded into the aggregate state, but is handed to
/// <see cref="StockItem.RegisterConsumption"/> together with <paramref name="OccurredOn"/> so they travel
/// on <c>StockConsumedEvent</c> and feed the movements read model (card [E4] #33) and the consumption
/// report (card #31). The operator is the authenticated user (audit trail #57), never taken from the
/// payload.
/// <para>
/// <paramref name="PreferredBatchId"/> is the lot the operator picked to draw from first (card #111); it is
/// optional — when null the aggregate draws FEFO (first-expired-first-out) automatically. Even when supplied,
/// FEFO covers any remainder if the preferred batch does not hold the whole amount. The batch(es) actually
/// drawn and their cost are recorded on <c>StockConsumedEvent</c> for the cost report (card #109).
/// </para>
/// </remarks>
public sealed record RegisterConsumptionCommand(
    Guid StockItemId,
    decimal Quantity,
    string Unit,
    Guid? ExperimentId,
    DateOnly? OccurredOn,
    Guid? PreferredBatchId = null) : ICommand;

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
    private readonly InventoryAuditRecorder _audit;

    public RegisterConsumptionCommandHandler(
        IStockItemRepository stockItems,
        InventoryAuditRecorder audit)
    {
        _stockItems = stockItems;
        _audit = audit;
    }

    public async Task<Unit> HandleAsync(
        RegisterConsumptionCommand request,
        CancellationToken cancellationToken = default)
    {
        StockItem item = await _stockItems.FindByIdAsync(request.StockItemId, cancellationToken)
            ?? throw new NotFoundException("StockItem", request.StockItemId);

        Quantity consumed = Quantity.Of(request.Quantity, UnitOfMeasure.FromSymbol(request.Unit));

        item.RegisterConsumption(consumed, request.OccurredOn, request.ExperimentId, request.PreferredBatchId);

        await _stockItems.UpdateAsync(item, cancellationToken);

        // Controlled substances leave a compliance trail (card #57); ordinary items do not.
        if (item.IsControlled)
        {
            await _audit.RecordStockItemAsync(
                item.CompanyId,
                item.Id,
                InventoryAuditActions.Consumption,
                new
                {
                    request.Quantity,
                    request.Unit,
                    request.ExperimentId,
                    request.OccurredOn,
                    RemainingBalance = item.Quantity.Value
                },
                cancellationToken);
        }

        return Unit.Value;
    }
}
