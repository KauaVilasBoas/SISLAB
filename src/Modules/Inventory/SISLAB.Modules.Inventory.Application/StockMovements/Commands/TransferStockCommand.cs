using FluentValidation;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Application.StockMovements.Commands;

/// <summary>
/// Transfers an item to another storage location. The handler orchestrates both aggregates: it asks the
/// destination location whether it may hold the item (active, and controlled-storage rule) before moving
/// it, so the controlled/location-type invariant lives with the aggregate that knows the location type
/// (card [E3] #23). The item balance is unchanged; only its location reference moves.
/// </summary>
/// <remarks>
/// The operator is the authenticated user (audit trail #57), never taken from the payload.
/// </remarks>
public sealed record TransferStockCommand(
    Guid StockItemId,
    Guid FromLocationId,
    Guid ToLocationId,
    DateOnly? OccurredOn) : ICommand;

internal sealed class TransferStockCommandValidator : AbstractValidator<TransferStockCommand>
{
    public TransferStockCommandValidator(IClock clock)
    {
        RuleFor(command => command.StockItemId).NotEmpty();
        RuleFor(command => command.FromLocationId).NotEmpty();
        RuleFor(command => command.ToLocationId).NotEmpty()
            .NotEqual(command => command.FromLocationId)
            .WithMessage("The destination location must differ from the origin location.");

        RuleFor(command => command.OccurredOn!.Value)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(clock.UtcNow))
            .When(command => command.OccurredOn.HasValue)
            .WithMessage("The operation date cannot be in the future.");
    }
}

internal sealed class TransferStockCommandHandler : ICommandHandler<TransferStockCommand>
{
    private readonly IStockItemRepository _stockItems;
    private readonly IStorageLocationRepository _storageLocations;

    public TransferStockCommandHandler(
        IStockItemRepository stockItems,
        IStorageLocationRepository storageLocations)
    {
        _stockItems = stockItems;
        _storageLocations = storageLocations;
    }

    public async Task<Unit> HandleAsync(
        TransferStockCommand request,
        CancellationToken cancellationToken = default)
    {
        StockItem item = await _stockItems.FindByIdAsync(request.StockItemId, cancellationToken)
            ?? throw new NotFoundException("StockItem", request.StockItemId);

        if (item.StorageLocationId != request.FromLocationId)
            throw new BusinessException(
                $"Item '{item.Name}' is not currently stored at the informed origin location.");

        StorageLocation destination =
            await _storageLocations.FindByIdAsync(request.ToLocationId, cancellationToken)
            ?? throw new NotFoundException("StorageLocation", request.ToLocationId);

        destination.EnsureCanStore(item);
        item.TransferTo(request.ToLocationId, request.OccurredOn);

        await _stockItems.UpdateAsync(item, cancellationToken);

        return Unit.Value;
    }
}
