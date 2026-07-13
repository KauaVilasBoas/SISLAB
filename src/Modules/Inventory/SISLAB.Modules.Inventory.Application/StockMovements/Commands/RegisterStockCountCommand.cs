using FluentValidation;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Application.StockMovements.Commands;

/// <summary>
/// Records a physical stock count (conference) of a controlled item — the periodic inventory of
/// controlled substances. It compares the counted balance with the system balance and registers the
/// divergence for compliance, <b>without changing the balance</b> (decision recorded on card [E3] #24);
/// corrections follow the normal entry or disposal flow. Returns the divergence (counted minus system
/// balance). The append-only audit record of type <c>Conference</c> is owned by card #57.
/// </summary>
/// <remarks>
/// The operator is the authenticated user (audit trail #57), never taken from the payload.
/// </remarks>
public sealed record RegisterStockCountCommand(
    Guid StockItemId,
    decimal CountedQuantity,
    string Unit,
    DateOnly? OccurredOn) : ICommand<decimal>;

internal sealed class RegisterStockCountCommandValidator : AbstractValidator<RegisterStockCountCommand>
{
    public RegisterStockCountCommandValidator(IClock clock)
    {
        RuleFor(command => command.StockItemId).NotEmpty();
        RuleFor(command => command.CountedQuantity).GreaterThanOrEqualTo(0m);
        RuleFor(command => command.Unit).NotEmpty();

        RuleFor(command => command.OccurredOn!.Value)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(clock.UtcNow))
            .When(command => command.OccurredOn.HasValue)
            .WithMessage("The operation date cannot be in the future.");
    }
}

internal sealed class RegisterStockCountCommandHandler : ICommandHandler<RegisterStockCountCommand, decimal>
{
    private readonly IStockItemRepository _stockItems;

    public RegisterStockCountCommandHandler(IStockItemRepository stockItems)
        => _stockItems = stockItems;

    public async Task<decimal> HandleAsync(
        RegisterStockCountCommand request,
        CancellationToken cancellationToken = default)
    {
        StockItem item = await _stockItems.FindByIdAsync(request.StockItemId, cancellationToken)
            ?? throw new NotFoundException("StockItem", request.StockItemId);

        Quantity counted = Quantity.Of(request.CountedQuantity, UnitOfMeasure.FromSymbol(request.Unit));

        decimal divergence = item.RegisterStockCount(counted);

        await _stockItems.UpdateAsync(item, cancellationToken);

        return divergence;
    }
}
