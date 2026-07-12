using SISLAB.Modules.Inventory.Application.StockRead;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.Modules.Inventory.Tests.Domain.ValueObjects;

namespace SISLAB.Modules.Inventory.Tests.Application.StockRead;

/// <summary>
/// Covers the read-side expiry classification (card [E4] #29). <see cref="ExpiryStatusRule"/> is the single
/// specification the item-listing SQL mirrors, so these tests lock its boundary conditions and assert it
/// agrees with the domain's <c>ExpiryDate.GetStatus</c> — keeping the C# read model and the SQL projection
/// in lockstep without a live database. (The SQL itself was smoke-tested against PostgreSQL.)
/// </summary>
public sealed class ExpiryStatusRuleTests
{
    private static readonly DateOnly Today = new(2026, 7, 12);

    [Fact]
    public void No_validity_is_not_applicable()
    {
        Assert.Equal(ExpiryStatusView.NotApplicable, ExpiryStatusRule.Classify(null, null, Today));
        Assert.Equal(ExpiryStatusView.NotApplicable, ExpiryStatusRule.Classify(2027, null, Today));
        Assert.Equal(ExpiryStatusView.NotApplicable, ExpiryStatusRule.Classify(null, 6, Today));
    }

    [Fact]
    public void Validity_well_in_the_future_is_ok()
        => Assert.Equal(ExpiryStatusView.Ok, ExpiryStatusRule.Classify(2027, 6, Today));

    [Fact]
    public void Validity_within_the_warning_window_is_expiring_soon()
        // Last valid day 2026-07-31 is within 30 days of 2026-07-12.
        => Assert.Equal(ExpiryStatusView.ExpiringSoon, ExpiryStatusRule.Classify(2026, 7, Today));

    [Fact]
    public void Validity_past_its_last_valid_day_is_expired()
        => Assert.Equal(ExpiryStatusView.Expired, ExpiryStatusRule.Classify(2025, 1, Today));

    [Fact]
    public void Item_is_valid_through_the_last_day_of_its_expiry_month()
    {
        // On the last valid day itself the item is not yet expired (expiring soon, since it is <= today+30).
        var lastDayOfJuly = new DateOnly(2026, 7, 31);
        Assert.Equal(ExpiryStatusView.ExpiringSoon, ExpiryStatusRule.Classify(2026, 7, lastDayOfJuly));

        // The next day it is expired.
        var firstOfAugust = new DateOnly(2026, 8, 1);
        Assert.Equal(ExpiryStatusView.Expired, ExpiryStatusRule.Classify(2026, 7, firstOfAugust));
    }

    [Theory]
    [InlineData(2027, 6)]  // ok
    [InlineData(2026, 7)]  // expiring soon
    [InlineData(2025, 1)]  // expired
    public void Matches_the_domain_expiry_classification(int year, int month)
    {
        ExpiryStatus domainStatus = ExpiryDate
            .FromYearMonth(year, month)
            .GetStatus(FixedClock.On(Today.Year, Today.Month, Today.Day), TimeSpan.FromDays(30));

        ExpiryStatusView readStatus = ExpiryStatusRule.Classify(year, month, Today);

        Assert.Equal(MapToView(domainStatus), readStatus);
    }

    private static ExpiryStatusView MapToView(ExpiryStatus status) => status switch
    {
        ExpiryStatus.Ok => ExpiryStatusView.Ok,
        ExpiryStatus.ExpiringSoon => ExpiryStatusView.ExpiringSoon,
        ExpiryStatus.Expired => ExpiryStatusView.Expired,
        _ => ExpiryStatusView.NotApplicable
    };
}
