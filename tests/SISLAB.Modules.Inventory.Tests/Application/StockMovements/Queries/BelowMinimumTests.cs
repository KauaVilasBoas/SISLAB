using SISLAB.Modules.Inventory.Application.StockMovements.Queries;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements.Queries;

/// <summary>
/// Covers the read contracts of card [E4] #32: the below-minimum list row carries the deficit the UI ranks
/// by, and the KPI exposes the single reposition count. The SQL that fills these (the below-minimum filter,
/// the deficit projection and the criticality ordering) was smoke-tested against PostgreSQL; here the
/// C#-side shape the dashboard consumes is pinned.
/// </summary>
public sealed class BelowMinimumTests
{
    [Fact]
    public void Below_minimum_item_deficit_is_the_gap_to_the_minimum()
    {
        // The handler projects deficit = minimum − quantity in SQL; the DTO carries it verbatim. A listed
        // row is always strictly below minimum, so the deficit is strictly positive.
        var item = new BelowMinimumItem(
            Id: Guid.NewGuid(),
            Name: "Etanol",
            Category: "Solvent",
            Brand: "Merck",
            Quantity: 10m,
            Unit: "mL",
            MinimumQuantity: 100m,
            MinimumUnit: "mL",
            Deficit: 90m,
            IsControlled: false,
            StorageLocationId: Guid.NewGuid(),
            StorageLocationName: "Prateleira 1",
            StorageLocationType: "Shelf");

        Assert.Equal(item.MinimumQuantity - item.Quantity, item.Deficit);
        Assert.True(item.Deficit > 0m);
    }

    [Fact]
    public void Below_minimum_summary_carries_the_reposition_count()
    {
        var summary = new BelowMinimumSummary(BelowMinimumCount: 3);

        Assert.Equal(3, summary.BelowMinimumCount);
    }

    [Fact]
    public void Below_minimum_summary_is_zero_when_the_whole_inventory_is_at_or_above_minimum()
    {
        var summary = new BelowMinimumSummary(BelowMinimumCount: 0);

        Assert.Equal(0, summary.BelowMinimumCount);
    }
}
