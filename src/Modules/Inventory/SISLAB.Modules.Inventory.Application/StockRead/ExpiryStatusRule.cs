namespace SISLAB.Modules.Inventory.Application.StockRead;

/// <summary>
/// Read-side classification of a stock item's month-granularity validity into an
/// <see cref="ExpiryStatusView"/>, against a reference day and warning window. It is the single
/// specification of the rule the item-listing query derives (card [E4] #29): the SQL <c>CASE</c> in
/// <see cref="ListStockItemsQuery"/> is a faithful mirror of this method, and the same last-valid-day
/// semantics as the domain's <c>ExpiryDate.GetStatus</c> — an item is valid through the last day of its
/// expiry month.
/// </summary>
/// <remarks>
/// Kept as a pure function (no clock, no I/O) so the rule is unit-testable without a live database and so
/// both the C# read model and the SQL projection agree on the exact boundary conditions (expired the day
/// after the last valid day; expiring-soon when that day falls within the window from <c>today</c>).
/// </remarks>
internal static class ExpiryStatusRule
{
    /// <summary>Default warning window that classifies an item as "expiring soon" — mirrors the domain default.</summary>
    internal const int DefaultWarningWindowDays = 30;

    /// <summary>
    /// Classifies the validity given by <paramref name="expiryYear"/>/<paramref name="expiryMonth"/>
    /// against <paramref name="today"/>. Returns <see cref="ExpiryStatusView.NotApplicable"/> when either
    /// component is null (no recorded validity).
    /// </summary>
    internal static ExpiryStatusView Classify(
        int? expiryYear,
        int? expiryMonth,
        DateOnly today,
        int warningWindowDays = DefaultWarningWindowDays)
    {
        if (expiryYear is not { } year || expiryMonth is not { } month)
            return ExpiryStatusView.NotApplicable;

        DateOnly lastValidDay = LastDayOfMonth(year, month);

        if (today > lastValidDay)
            return ExpiryStatusView.Expired;

        DateOnly warningThreshold = today.AddDays(warningWindowDays);
        return lastValidDay <= warningThreshold
            ? ExpiryStatusView.ExpiringSoon
            : ExpiryStatusView.Ok;
    }

    private static DateOnly LastDayOfMonth(int year, int month)
        => new(year, month, DateTime.DaysInMonth(year, month));
}
