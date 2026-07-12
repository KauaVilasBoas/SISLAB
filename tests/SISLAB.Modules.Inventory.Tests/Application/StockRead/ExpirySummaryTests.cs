using SISLAB.Modules.Inventory.Application.StockRead;

namespace SISLAB.Modules.Inventory.Tests.Application.StockRead;

/// <summary>
/// Covers the donut contract of card [E4] #30: <see cref="ExpirySummary"/> exposes exactly three counts
/// (Expired / ExpiringSoon / Ok) and their sum as <see cref="ExpirySummary.Total"/>. Items with no validity
/// are, by design, not a slice — they never contribute to any count, so the whole (Total) is the number of
/// items that carry a validity, not the whole inventory. The SQL that fills these counts was smoke-tested
/// against PostgreSQL; here the C#-side contract the UI renders slice percentages from is pinned.
/// </summary>
public sealed class ExpirySummaryTests
{
    [Fact]
    public void Total_is_the_sum_of_the_three_slices()
    {
        var summary = new ExpirySummary(Expired: 4, ExpiringSoon: 3, Ok: 10);

        Assert.Equal(17, summary.Total);
    }

    [Fact]
    public void Total_is_zero_when_no_item_carries_a_validity()
    {
        var summary = new ExpirySummary(Expired: 0, ExpiringSoon: 0, Ok: 0);

        Assert.Equal(0, summary.Total);
    }
}
