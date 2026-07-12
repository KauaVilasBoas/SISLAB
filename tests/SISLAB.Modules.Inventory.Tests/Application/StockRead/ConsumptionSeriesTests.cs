using SISLAB.Modules.Inventory.Application.StockRead;

namespace SISLAB.Modules.Inventory.Tests.Application.StockRead;

/// <summary>
/// Covers the pure logic of the consumption series (card [E4] #31): the % delta versus the preceding period,
/// the same-length previous-window derivation, and the day/month granularity rule. The SQL that fills the
/// buckets and per-unit totals was smoke-tested against PostgreSQL; here the rules the chart depends on are
/// pinned in isolation.
/// </summary>
public sealed class ConsumptionSeriesTests
{
    // --- Delta ------------------------------------------------------------------------------------------

    [Fact]
    public void Delta_is_the_signed_percentage_change_from_the_previous_period()
    {
        // 120 now vs 100 before → +20%.
        Assert.Equal(20m, ConsumptionDelta.Compute(current: 120m, previous: 100m));
    }

    [Fact]
    public void Delta_is_negative_when_consumption_dropped()
    {
        // 75 now vs 100 before → -25%.
        Assert.Equal(-25m, ConsumptionDelta.Compute(current: 75m, previous: 100m));
    }

    [Fact]
    public void Delta_is_zero_when_consumption_is_flat()
    {
        Assert.Equal(0m, ConsumptionDelta.Compute(current: 100m, previous: 100m));
    }

    [Fact]
    public void Delta_is_undefined_when_there_was_no_previous_consumption()
    {
        // No base to compare against → null (the UI shows "novo"/"—", not a fake +100%).
        Assert.Null(ConsumptionDelta.Compute(current: 50m, previous: 0m));
    }

    [Fact]
    public void Delta_is_undefined_when_both_periods_are_empty()
    {
        Assert.Null(ConsumptionDelta.Compute(current: 0m, previous: 0m));
    }

    [Fact]
    public void Delta_is_minus_one_hundred_percent_when_consumption_fell_to_zero()
    {
        Assert.Equal(-100m, ConsumptionDelta.Compute(current: 0m, previous: 100m));
    }

    // --- Previous window --------------------------------------------------------------------------------

    [Fact]
    public void Previous_window_is_the_same_length_period_immediately_before_the_current_one()
    {
        // June (30 inclusive days) → the previous 30 days end May 31 and start May 2.
        (DateOnly previousFrom, DateOnly previousTo) =
            GetConsumptionSeriesQueryHandler.PreviousWindow(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

        Assert.Equal(new DateOnly(2026, 5, 31), previousTo);
        Assert.Equal(new DateOnly(2026, 5, 2), previousFrom);
    }

    [Fact]
    public void Previous_window_of_a_single_day_is_the_day_before()
    {
        DateOnly day = new(2026, 6, 15);

        (DateOnly previousFrom, DateOnly previousTo) =
            GetConsumptionSeriesQueryHandler.PreviousWindow(day, day);

        Assert.Equal(new DateOnly(2026, 6, 14), previousFrom);
        Assert.Equal(new DateOnly(2026, 6, 14), previousTo);
    }

    [Fact]
    public void Previous_window_preserves_the_exact_inclusive_length_of_a_seven_day_window()
    {
        DateOnly from = new(2026, 6, 24);
        DateOnly to = new(2026, 6, 30); // 7 inclusive days

        (DateOnly previousFrom, DateOnly previousTo) =
            GetConsumptionSeriesQueryHandler.PreviousWindow(from, to);

        Assert.Equal(new DateOnly(2026, 6, 23), previousTo);            // day before the current start
        Assert.Equal(new DateOnly(2026, 6, 17), previousFrom);          // 7 inclusive days back
        Assert.Equal(6, previousTo.DayNumber - previousFrom.DayNumber); // 7 inclusive days
    }

    // --- Bucket granularity -----------------------------------------------------------------------------

    [Theory]
    [InlineData("2026-06-24", "2026-06-30", ConsumptionBucket.Day)]   // 7-day window  → day
    [InlineData("2026-06-01", "2026-06-30", ConsumptionBucket.Day)]   // 30-day window → day
    [InlineData("2026-04-01", "2026-06-30", ConsumptionBucket.Month)] // 3-month window → month
    public void Bucket_is_derived_from_the_window_width(string from, string to, ConsumptionBucket expected)
    {
        ConsumptionBucket bucket =
            GetConsumptionSeriesQueryHandler.DeriveBucket(DateOnly.Parse(from), DateOnly.Parse(to));

        Assert.Equal(expected, bucket);
    }

    [Fact]
    public void An_explicit_bucket_overrides_the_derived_one()
    {
        // A 30-day window would derive Day; the caller pins Month.
        var query = new GetConsumptionSeriesQuery
        {
            From = new DateOnly(2026, 6, 1),
            To = new DateOnly(2026, 6, 30),
            Bucket = ConsumptionBucket.Month
        };

        Assert.Equal(ConsumptionBucket.Month, GetConsumptionSeriesQueryHandler.ResolveBucket(query));
    }

    [Fact]
    public void The_derived_bucket_is_used_when_the_caller_pins_none()
    {
        var query = new GetConsumptionSeriesQuery
        {
            From = new DateOnly(2026, 6, 1),
            To = new DateOnly(2026, 6, 30)
        };

        Assert.Equal(ConsumptionBucket.Day, GetConsumptionSeriesQueryHandler.ResolveBucket(query));
    }

    // --- Result contract --------------------------------------------------------------------------------

    [Fact]
    public void Period_total_carries_the_current_and_previous_totals_with_the_computed_delta()
    {
        var total = new ConsumptionPeriodTotal(
            Unit: "mL",
            CurrentTotal: 120m,
            PreviousTotal: 100m,
            DeltaPercentage: ConsumptionDelta.Compute(120m, 100m));

        Assert.Equal("mL", total.Unit);
        Assert.Equal(20m, total.DeltaPercentage);
    }
}
