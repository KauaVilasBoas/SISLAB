using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.ValueObjects;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements.Commands;

/// <summary>Builds <see cref="StockItem"/> instances for handler tests, at a caller-known location.</summary>
internal static class StockItemFactory
{
    public static readonly UnitOfMeasure Ml = UnitOfMeasure.Milliliter;

    /// <summary>
    /// A fixed per-tenant category id the aggregate references by value (card [E12] #76). The aggregate no
    /// longer knows the category's name or controlled flag — that lives in the Configuration module — so a
    /// stable id is all a fixture needs.
    /// </summary>
    public static readonly Guid CategoryId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    public static StockItem At(
        Guid locationId,
        decimal initial = 100m,
        decimal minimum = 10m,
        bool isControlled = false) =>
        StockItem.Register(
            name: "DMSO",
            categoryId: CategoryId,
            storageLocationId: locationId,
            initialQuantity: Quantity.Of(initial, Ml),
            minimumQuantity: Quantity.Of(minimum, Ml),
            isControlled: isControlled);
}
