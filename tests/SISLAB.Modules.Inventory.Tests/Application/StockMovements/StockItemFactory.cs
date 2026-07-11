using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.ValueObjects;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements;

/// <summary>Builds <see cref="StockItem"/> instances for handler tests, at a caller-known location.</summary>
internal static class StockItemFactory
{
    public static readonly UnitOfMeasure Ml = UnitOfMeasure.Milliliter;

    public static StockItem At(
        Guid locationId,
        decimal initial = 100m,
        decimal minimum = 10m,
        bool isControlled = false) =>
        StockItem.Register(
            name: "DMSO",
            category: isControlled ? StockItemCategory.ControlledAnesthetic : StockItemCategory.Solvent,
            storageLocationId: locationId,
            initialQuantity: Quantity.Of(initial, Ml),
            minimumQuantity: Quantity.Of(minimum, Ml),
            isControlled: isControlled);
}
