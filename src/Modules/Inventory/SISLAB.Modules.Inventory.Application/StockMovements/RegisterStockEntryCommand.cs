using FluentValidation;
using SISLAB.Modules.Inventory.Domain.Partners;
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
/// by the audit trail (card #57) — decision recorded on card [E3] #24. <paramref name="OccurredOn"/> and
/// <paramref name="SupplierPartnerId"/> are origin/traceability metadata: they are not folded into the
/// aggregate state, but are handed to <see cref="StockItem.RegisterEntry"/> so they travel on
/// <c>StockReceivedEvent</c> and feed the movements read model (card [E4] #33). When
/// <paramref name="SupplierPartnerId"/> is supplied, the handler verifies the partner exists and may
/// supply (card [E3] #28) before applying the entry.
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
    private readonly IPartnerRepository _partners;

    public RegisterStockEntryCommandHandler(
        IStockItemRepository stockItems,
        IPartnerRepository partners)
    {
        _stockItems = stockItems;
        _partners = partners;
    }

    public async Task<Guid> HandleAsync(
        RegisterStockEntryCommand request,
        CancellationToken cancellationToken = default)
    {
        StockItem item = await _stockItems.FindByIdAsync(request.StockItemId, cancellationToken)
            ?? throw new NotFoundException("StockItem", request.StockItemId);

        await EnsureSupplierCanSupplyAsync(request.SupplierPartnerId, cancellationToken);

        Quantity received = Quantity.Of(request.Quantity, UnitOfMeasure.FromSymbol(request.Unit));
        Lot? lot = Lot.FromCode(request.LotCode);
        ExpiryDate? expiry = request.ExpiryYear.HasValue && request.ExpiryMonth.HasValue
            ? ExpiryDate.FromYearMonth(request.ExpiryYear.Value, request.ExpiryMonth.Value)
            : null;

        item.RegisterEntry(received, lot, expiry, request.OccurredOn, request.SupplierPartnerId);

        await _stockItems.UpdateAsync(item, cancellationToken);

        return item.Id;
    }

    /// <summary>
    /// When a supplier is informed, loads the partner and asks it whether it may be the origin of the
    /// entry. The supply invariant lives with the <see cref="Partner"/> aggregate (card [E3] #28); an
    /// unknown id is a <see cref="NotFoundException"/>, a non-supplier/inactive partner a
    /// <see cref="BusinessException"/> (raised by <see cref="Partner.EnsureCanSupply"/>).
    /// </summary>
    private async Task EnsureSupplierCanSupplyAsync(
        Guid? supplierPartnerId,
        CancellationToken cancellationToken)
    {
        if (supplierPartnerId is not { } partnerId)
            return;

        Partner supplier = await _partners.FindByIdAsync(partnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", partnerId);

        supplier.EnsureCanSupply();
    }
}
