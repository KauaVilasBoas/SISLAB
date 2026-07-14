using FluentValidation;
using SISLAB.Modules.Configuration.Contracts;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.StockMovements.Commands;

/// <summary>
/// Registers a new stock item for the active company with its initial balance (card [E3] #25 / [E12] #76).
/// The item's category is referenced <b>by value</b> (<paramref name="CategoryId"/>) — a per-tenant
/// Configuration category — and the storage location likewise. The handler validates both cross-module
/// references before creating the aggregate.
/// </summary>
/// <remarks>
/// <b>Category validation (card [E12] #76).</b> Before building the aggregate, the handler verifies through
/// <see cref="ILabConfiguration"/> that <paramref name="CategoryId"/> is a real category of the active tenant
/// — the same guard pattern the entry command uses for the supplier: an unknown category is a
/// <see cref="NotFoundException"/>. This keeps the "category exists and belongs to the tenant" invariant out
/// of the aggregate (which only holds the id by value) and in the write-side, exactly like the supplier and
/// storage-location guards.
/// </remarks>
public sealed record RegisterStockItemCommand(
    string Name,
    Guid CategoryId,
    Guid StorageLocationId,
    decimal InitialQuantity,
    decimal MinimumQuantity,
    string Unit,
    bool IsControlled,
    string? Brand,
    string? Application,
    string? LotCode,
    int? ExpiryYear,
    int? ExpiryMonth) : ICommand<Guid>;

internal sealed class RegisterStockItemCommandValidator : AbstractValidator<RegisterStockItemCommand>
{
    public RegisterStockItemCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.CategoryId).NotEmpty();
        RuleFor(command => command.StorageLocationId).NotEmpty();
        RuleFor(command => command.InitialQuantity).GreaterThanOrEqualTo(0m);
        RuleFor(command => command.MinimumQuantity).GreaterThanOrEqualTo(0m);
        RuleFor(command => command.Unit).NotEmpty();

        When(command => command.ExpiryYear.HasValue || command.ExpiryMonth.HasValue, () =>
        {
            RuleFor(command => command.ExpiryYear).NotNull().GreaterThan(0);
            RuleFor(command => command.ExpiryMonth).NotNull().InclusiveBetween(1, 12);
        });
    }
}

internal sealed class RegisterStockItemCommandHandler : ICommandHandler<RegisterStockItemCommand, Guid>
{
    private readonly IStockItemRepository _stockItems;
    private readonly IStorageLocationRepository _storageLocations;
    private readonly ILabConfiguration _labConfiguration;

    public RegisterStockItemCommandHandler(
        IStockItemRepository stockItems,
        IStorageLocationRepository storageLocations,
        ILabConfiguration labConfiguration)
    {
        _stockItems = stockItems;
        _storageLocations = storageLocations;
        _labConfiguration = labConfiguration;
    }

    public async Task<Guid> HandleAsync(
        RegisterStockItemCommand request,
        CancellationToken cancellationToken = default)
    {
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        await EnsureStorageLocationExistsAsync(request.StorageLocationId, cancellationToken);

        UnitOfMeasure unit = UnitOfMeasure.FromSymbol(request.Unit);
        Quantity initial = Quantity.Of(request.InitialQuantity, unit);
        Quantity minimum = Quantity.Of(request.MinimumQuantity, unit);
        Lot? lot = Lot.FromCode(request.LotCode);
        ExpiryDate? expiry = request.ExpiryYear.HasValue && request.ExpiryMonth.HasValue
            ? ExpiryDate.FromYearMonth(request.ExpiryYear.Value, request.ExpiryMonth.Value)
            : null;

        StockItem item = StockItem.Register(
            request.Name,
            request.CategoryId,
            request.StorageLocationId,
            initial,
            minimum,
            request.IsControlled,
            ContainerState.Closed,
            request.Brand,
            request.Application,
            lot,
            expiry);

        await _stockItems.AddAsync(item, cancellationToken);

        return item.Id;
    }

    /// <summary>
    /// Verifies the category exists for the active tenant through the Configuration boundary (card [E12] #76).
    /// The category invariant lives in the Configuration module; an unknown id is a
    /// <see cref="NotFoundException"/> — same guard shape as the supplier check on the entry command.
    /// </summary>
    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        bool exists = await _labConfiguration.ItemCategoryExistsAsync(categoryId, cancellationToken);
        if (!exists)
            throw new NotFoundException("ItemCategory", categoryId);
    }

    /// <summary>Verifies the storage location exists for the active tenant (implicitly tenant-scoped).</summary>
    private async Task EnsureStorageLocationExistsAsync(Guid storageLocationId, CancellationToken cancellationToken)
    {
        StorageLocation? location = await _storageLocations.FindByIdAsync(storageLocationId, cancellationToken);
        if (location is null)
            throw new NotFoundException("StorageLocation", storageLocationId);
    }
}
