using FluentValidation;
using SISLAB.Modules.Configuration.Contracts;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.StockMovements.Commands;

/// <summary>
/// Corrects the descriptive metadata of an existing stock item (card [E7] #46): its name, category, storage
/// location, minimum-stock threshold and the optional brand/application text. This is deliberately a
/// <b>conservative</b> update — it never touches the on-hand balance, the lot, the expiry nor the unit of
/// measure, and it emits no stock movement: those are the province of the entry/consumption/transfer/disposal
/// operations and their ledger. Passing a null or blank brand/application clears it.
/// </summary>
/// <remarks>
/// The unit of measure is intentionally not editable: it is fixed at creation by the item's balance, and the
/// movement history is recorded in that unit — changing it would require converting the balance and would
/// corrupt past movements. The new minimum therefore reuses the item's current unit. Correcting a wrong unit
/// is a dispose-and-recreate flow, not an edit.
///
/// <para>
/// Cross-module references are validated the same way the create command does: the category is checked through
/// <see cref="ILabConfiguration"/> and the storage location through its repository. An unknown id — for either —
/// is a <see cref="NotFoundException"/>, keeping the "belongs to the tenant" invariant in the write-side rather
/// than in the aggregate (which holds both ids by value).
/// </para>
/// </remarks>
public sealed record UpdateStockItemCommand(
    Guid StockItemId,
    string Name,
    Guid CategoryId,
    Guid StorageLocationId,
    decimal MinimumQuantity,
    string? Brand,
    string? Application) : ICommand;

internal sealed class UpdateStockItemCommandValidator : AbstractValidator<UpdateStockItemCommand>
{
    public UpdateStockItemCommandValidator()
    {
        RuleFor(command => command.StockItemId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.CategoryId).NotEmpty();
        RuleFor(command => command.StorageLocationId).NotEmpty();
        RuleFor(command => command.MinimumQuantity).GreaterThanOrEqualTo(0m);
        RuleFor(command => command.Brand).MaximumLength(120);
        RuleFor(command => command.Application).MaximumLength(500);
    }
}

internal sealed class UpdateStockItemCommandHandler : ICommandHandler<UpdateStockItemCommand>
{
    private readonly IStockItemRepository _stockItems;
    private readonly IStorageLocationRepository _storageLocations;
    private readonly ILabConfiguration _labConfiguration;

    public UpdateStockItemCommandHandler(
        IStockItemRepository stockItems,
        IStorageLocationRepository storageLocations,
        ILabConfiguration labConfiguration)
    {
        _stockItems = stockItems;
        _storageLocations = storageLocations;
        _labConfiguration = labConfiguration;
    }

    public async Task<Unit> HandleAsync(
        UpdateStockItemCommand request,
        CancellationToken cancellationToken = default)
    {
        StockItem item = await _stockItems.FindByIdAsync(request.StockItemId, cancellationToken)
            ?? throw new NotFoundException("StockItem", request.StockItemId);

        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        await EnsureStorageLocationExistsAsync(request.StorageLocationId, cancellationToken);

        // The new threshold reuses the item's current unit — the unit is fixed at creation and never edited.
        Quantity minimum = Quantity.Of(request.MinimumQuantity, item.MinimumQuantity.Unit);

        item.Rename(request.Name);
        item.Recategorize(request.CategoryId);
        item.Relocate(request.StorageLocationId);
        item.AdjustMinimumQuantity(minimum);
        item.Describe(request.Brand, request.Application);

        await _stockItems.UpdateAsync(item, cancellationToken);

        return Unit.Value;
    }

    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        bool exists = await _labConfiguration.ItemCategoryExistsAsync(categoryId, cancellationToken);
        if (!exists)
            throw new NotFoundException("ItemCategory", categoryId);
    }

    private async Task EnsureStorageLocationExistsAsync(Guid storageLocationId, CancellationToken cancellationToken)
    {
        StorageLocation? location = await _storageLocations.FindByIdAsync(storageLocationId, cancellationToken);
        if (location is null)
            throw new NotFoundException("StorageLocation", storageLocationId);
    }
}
