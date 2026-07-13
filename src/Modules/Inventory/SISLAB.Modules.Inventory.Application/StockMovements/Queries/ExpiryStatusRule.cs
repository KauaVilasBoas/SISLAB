namespace SISLAB.Modules.Inventory.Application.StockMovements.Queries;

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

    /// <summary>
    /// Decides whether a classified item is "at risk" for the expiry listing (card [E4] #30): an
    /// <see cref="ExpiryStatusView.ExpiringSoon"/> item always is, an <see cref="ExpiryStatusView.Expired"/>
    /// one only when <paramref name="includeExpired"/> is set, and <see cref="ExpiryStatusView.Ok"/> /
    /// <see cref="ExpiryStatusView.NotApplicable"/> items never are. This is the single C# statement of the
    /// inclusion predicate the listing SQL's outer <c>WHERE</c> mirrors, so both agree without duplicating
    /// the 30-day window (which lives only in <see cref="Classify"/>). Reusable by the E6 validity job (#41).
    /// </summary>
    internal static bool IsAtRisk(ExpiryStatusView status, bool includeExpired) => status switch
    {
        ExpiryStatusView.ExpiringSoon => true,
        ExpiryStatusView.Expired => includeExpired,
        _ => false
    };

    private static DateOnly LastDayOfMonth(int year, int month)
        => new(year, month, DateTime.DaysInMonth(year, month));
}
