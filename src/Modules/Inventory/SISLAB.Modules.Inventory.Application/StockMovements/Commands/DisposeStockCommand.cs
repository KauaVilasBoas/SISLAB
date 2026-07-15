using FluentValidation;
using SISLAB.Modules.Inventory.Application.Audit;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Application.StockMovements.Commands;

/// <summary>
/// Discards a quantity of stock (for example an expired or unusable batch), decreasing the item balance.
/// This is an auditable operation — an append-only record is kept, especially for controlled items
/// (card [E3] #24, audit trail #57).
/// </summary>
/// <remarks>
/// <paramref name="Reason"/> (motivo, e.g. "expired") is the business justification kept for the audit
/// trail; it is carried on the command and not folded into the aggregate here. The operator is the
/// authenticated user (audit trail #57), never taken from the payload.
/// </remarks>
public sealed record DisposeStockCommand(
    Guid StockItemId,
    decimal Quantity,
    string Unit,
    string Reason,
    DateOnly? OccurredOn) : ICommand;

internal sealed class DisposeStockCommandValidator : AbstractValidator<DisposeStockCommand>
{
    public DisposeStockCommandValidator(IClock clock)
    {
        RuleFor(command => command.StockItemId).NotEmpty();
        RuleFor(command => command.Quantity).GreaterThan(0m);
        RuleFor(command => command.Unit).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(500);

        RuleFor(command => command.OccurredOn!.Value)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(clock.UtcNow))
            .When(command => command.OccurredOn.HasValue)
            .WithMessage("The operation date cannot be in the future.");
    }
}

internal sealed class DisposeStockCommandHandler : ICommandHandler<DisposeStockCommand>
{
    private readonly IStockItemRepository _stockItems;
    private readonly InventoryAuditRecorder _audit;

    public DisposeStockCommandHandler(
        IStockItemRepository stockItems,
        InventoryAuditRecorder audit)
    {
        _stockItems = stockItems;
        _audit = audit;
    }

    public async Task<Unit> HandleAsync(
        DisposeStockCommand request,
        CancellationToken cancellationToken = default)
    {
        StockItem item = await _stockItems.FindByIdAsync(request.StockItemId, cancellationToken)
            ?? throw new NotFoundException("StockItem", request.StockItemId);

        Quantity disposed = Quantity.Of(request.Quantity, UnitOfMeasure.FromSymbol(request.Unit));

        item.Dispose(disposed, request.OccurredOn);

        await _stockItems.UpdateAsync(item, cancellationToken);

        // Controlled substances leave a compliance trail (card #57); ordinary items do not.
        if (item.IsControlled)
        {
            await _audit.RecordStockItemAsync(
                item.CompanyId,
                item.Id,
                InventoryAuditActions.Disposal,
                new
                {
                    request.Quantity,
                    request.Unit,
                    request.Reason,
                    request.OccurredOn,
                    RemainingBalance = item.Quantity.Value
                },
                cancellationToken);
        }

        return Unit.Value;
    }
}
