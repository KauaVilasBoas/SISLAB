using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Domain.ValueObjects;

public sealed class ExpiryDateTests
{
    [Fact]
    public void FromYearMonth_keeps_month_granularity()
    {
        ExpiryDate expiry = ExpiryDate.FromYearMonth(2026, 12);

        Assert.Equal(2026, expiry.Year);
        Assert.Equal(12, expiry.Month);
        Assert.Equal("12/2026", expiry.ToString());
    }

    [Fact]
    public void Last_valid_day_is_the_last_day_of_the_month()
    {
        Assert.Equal(new DateOnly(2025, 2, 28), ExpiryDate.FromYearMonth(2025, 2).LastValidDay);
        Assert.Equal(new DateOnly(2024, 2, 29), ExpiryDate.FromYearMonth(2024, 2).LastValidDay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void FromYearMonth_rejects_out_of_range_month(int month)
    {
        Assert.Throws<DomainException>(() => ExpiryDate.FromYearMonth(2026, month));
    }

    [Fact]
    public void Item_is_valid_until_the_end_of_its_month()
    {
        ExpiryDate expiry = ExpiryDate.FromYearMonth(2026, 12);

        Assert.False(expiry.IsExpired(FixedClock.On(2026, 12, 31)));
    }

    [Fact]
    public void Item_is_expired_from_the_first_day_of_the_following_month()
    {
        ExpiryDate expiry = ExpiryDate.FromYearMonth(2026, 12);

        Assert.True(expiry.IsExpired(FixedClock.On(2027, 1, 1)));
    }

    [Fact]
    public void Status_is_expired_when_past_the_last_valid_day()
    {
        ExpiryStatus status = ExpiryDate.FromYearMonth(2025, 3)
            .GetStatus(FixedClock.On(2025, 4, 1), TimeSpan.FromDays(30));

        Assert.Equal(ExpiryStatus.Expired, status);
    }

    [Fact]
    public void Status_is_expiring_soon_within_the_warning_window()
    {
        ExpiryStatus status = ExpiryDate.FromYearMonth(2026, 3)
            .GetStatus(FixedClock.On(2026, 3, 20), TimeSpan.FromDays(30));

        Assert.Equal(ExpiryStatus.ExpiringSoon, status);
    }

    [Fact]
    public void Status_is_ok_beyond_the_warning_window()
    {
        ExpiryStatus status = ExpiryDate.FromYearMonth(2026, 12)
            .GetStatus(FixedClock.On(2026, 1, 1), TimeSpan.FromDays(30));

        Assert.Equal(ExpiryStatus.Ok, status);
    }

    [Fact]
    public void GetStatus_rejects_a_negative_warning_window()
    {
        Assert.Throws<DomainException>(
            () => ExpiryDate.FromYearMonth(2026, 1).GetStatus(FixedClock.On(2026, 1, 1), TimeSpan.FromDays(-1)));
    }

    [Fact]
    public void FromDate_reduces_a_full_date_to_year_and_month()
    {
        ExpiryDate expiry = ExpiryDate.FromDate(new DateOnly(2026, 7, 15));

        Assert.Equal(ExpiryDate.FromYearMonth(2026, 7), expiry);
    }

    [Fact]
    public void Expiry_dates_have_structural_equality()
    {
        Assert.Equal(ExpiryDate.FromYearMonth(2026, 5), ExpiryDate.FromYearMonth(2026, 5));
        Assert.NotEqual(ExpiryDate.FromYearMonth(2026, 5), ExpiryDate.FromYearMonth(2026, 6));
    }
}
