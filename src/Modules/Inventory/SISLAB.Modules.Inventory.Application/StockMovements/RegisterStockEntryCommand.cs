using FluentValidation;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Application.StockMovements;

/// <summary>
/// Registers an incoming stock entry (a receipt) on an existing item, increasing its balance and
/// refreshing the traceability data of the received batch (lot and expiry). The item to receive into
/// is identified by <paramref name="StockItemId"/>; item registration is a separate concern.
/// </summary>
/// <remarks>
/// The operator (responsável) is never taken from the payload: it is the authenticated user, captured
/// by the audit trail (card #57) — decision recorded on card [E3] #24. <paramref name="SupplierPartnerId"/>
/// and <paramref name="OccurredOn"/> are origin/traceability metadata for that same audit trail; they are
/// carried on the command but not folded into the aggregate here (owned by cards #28/#57).
/// </remarks>
public sealed record RegisterStockEntryCommand(
    Guid StockItemId,
    decimal Quantity,
    string Unit,
    string? LotCode,
    int? ExpiryYear,
    int? ExpiryMonth,
    Guid? SupplierPartnerId,
    DateOnly? OccurredOn) : ICommand<Guid>;

internal sealed class RegisterStockEntryCommandValidator : AbstractValidator<RegisterStockEntryCommand>
{
    public RegisterStockEntryCommandValidator(IClock clock)
    {
        RuleFor(command => command.StockItemId).NotEmpty();
        RuleFor(command => command.Quantity).GreaterThan(0m);
        RuleFor(command => command.Unit).NotEmpty();

        When(command => command.ExpiryYear.HasValue || command.ExpiryMonth.HasValue, () =>
        {
            RuleFor(command => command.ExpiryYear).NotNull().GreaterThan(0);
            RuleFor(command => command.ExpiryMonth).NotNull().InclusiveBetween(1, 12);
        });

        RuleFor(command => command.OccurredOn!.Value)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(clock.UtcNow))
            .When(command => command.OccurredOn.HasValue)
            .WithMessage("The operation date cannot be in the future.");
    }
}

internal sealed class RegisterStockEntryCommandHandler : ICommandHandler<RegisterStockEntryCommand, Guid>
{
    private readonly IStockItemRepository _stockItems;

    public RegisterStockEntryCommandHandler(IStockItemRepository stockItems)
        => _stockItems = stockItems;

    public async Task<Guid> HandleAsync(
        RegisterStockEntryCommand request,
        CancellationToken cancellationToken = default)
    {
        StockItem item = await _stockItems.FindByIdAsync(request.StockItemId, cancellationToken)
            ?? throw new NotFoundException("StockItem", request.StockItemId);

        Quantity received = Quantity.Of(request.Quantity, UnitOfMeasure.FromSymbol(request.Unit));
        Lot? lot = Lot.FromCode(request.LotCode);
        ExpiryDate? expiry = request.ExpiryYear.HasValue && request.ExpiryMonth.HasValue
            ? ExpiryDate.FromYearMonth(request.ExpiryYear.Value, request.ExpiryMonth.Value)
            : null;

        item.RegisterEntry(received, lot, expiry);

        await _stockItems.UpdateAsync(item, cancellationToken);

        return item.Id;
    }
}
