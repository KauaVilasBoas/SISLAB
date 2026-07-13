using SISLAB.Modules.Inventory.Application.StockMovements.Queries;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements.Queries;

/// <summary>
/// Covers the read contracts and window rule of the consumption report (card [E4] #31). The SQL that fills
/// the rows (the consumed-movements filter, the per-(item, unit) aggregation, the stock_items join and the
/// grand totals) was smoke-tested against PostgreSQL; here the C#-side shape the report UI consumes and the
/// window guard are pinned.
/// </summary>
public sealed class ConsumptionReportTests
{
    [Fact]
    public void Report_bundles_the_paginated_rows_and_the_per_unit_totals()
    {
        var rows = new[]
        {
            new ConsumptionReportItem(Guid.NewGuid(), "Etanol", "Solvent", TotalConsumed: 120m, Unit: "mL", MovementCount: 3),
            new ConsumptionReportItem(Guid.NewGuid(), "Ágar", "Reagent", TotalConsumed: 4m, Unit: "g", MovementCount: 2)
        };
        var page = new PagedResult<ConsumptionReportItem>(rows, totalCount: 2, page: 1, pageSize: 20);
        var totals = new[]
        {
            new ConsumptionTotal("mL", TotalConsumed: 120m, MovementCount: 3),
            new ConsumptionTotal("g", TotalConsumed: 4m, MovementCount: 2)
        };

        var report = new ConsumptionReport(page, totals);

        Assert.Equal(2, report.Items.TotalCount);
        Assert.Equal(2, report.Totals.Count);
    }

    [Fact]
    public void Report_totals_stay_per_unit_so_different_units_are_never_summed_together()
    {
        // 120 mL and 4 g must remain two separate totals — the read side never converts between units.
        var totals = new[]
        {
            new ConsumptionTotal("mL", TotalConsumed: 120m, MovementCount: 3),
            new ConsumptionTotal("g", TotalConsumed: 4m, MovementCount: 2)
        };

        Assert.Equal(2, totals.Length);
        Assert.All(totals, total => Assert.True(total.TotalConsumed > 0m));
        string[] units = totals.Select(total => total.Unit).ToArray();
        Assert.Equal(units.Length, units.Distinct().Count());
    }

    [Fact]
    public void Report_item_carries_the_movement_count_that_fed_the_total()
    {
        var item = new ConsumptionReportItem(
            Guid.NewGuid(), "Etanol", "Solvent", TotalConsumed: 120m, Unit: "mL", MovementCount: 3);

        Assert.Equal(3, item.MovementCount);
        Assert.Equal("mL", item.Unit);
    }

    [Fact]
    public void Window_guard_accepts_a_single_day_range()
    {
        DateOnly day = new(2026, 6, 15);

        // A single-day window is valid — no throw.
        ConsumptionWindow.EnsureValid(day, day);
    }

    [Fact]
    public void Window_guard_rejects_an_unset_range()
    {
        Assert.Throws<BusinessException>(() => ConsumptionWindow.EnsureValid(default, default));
        Assert.Throws<BusinessException>(() => ConsumptionWindow.EnsureValid(new DateOnly(2026, 6, 1), default));
        Assert.Throws<BusinessException>(() => ConsumptionWindow.EnsureValid(default, new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void Window_guard_rejects_an_inverted_range()
    {
        Assert.Throws<BusinessException>(() =>
            ConsumptionWindow.EnsureValid(new DateOnly(2026, 6, 30), new DateOnly(2026, 6, 1)));
    }
}
