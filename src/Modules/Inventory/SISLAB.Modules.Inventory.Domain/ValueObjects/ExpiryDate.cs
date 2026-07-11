using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Domain.ValueObjects;

/// <summary>
/// Expiry date of a stock item with month granularity, matching how the laboratory records validity
/// (for example "dez/2026"). An item expires at the end of its month: a validity of December 2026
/// remains valid through 2026-12-31 and is expired from 2027-01-01. The value object classifies
/// itself as expired, expiring soon or ok against a supplied <see cref="IClock"/>.
/// </summary>
public sealed class ExpiryDate : ValueObject
{
    private ExpiryDate(int year, int month)
    {
        Year = year;
        Month = month;
    }

    public int Year { get; }

    public int Month { get; }

    /// <summary>Last calendar day covered by this expiry (inclusive), i.e. the last day of the month.</summary>
    public DateOnly LastValidDay => new(Year, Month, DateTime.DaysInMonth(Year, Month));

    public static ExpiryDate FromYearMonth(int year, int month)
    {
        if (year < 1)
            throw new DomainException($"Expiry year is out of range. Received: {year}.");

        if (month is < 1 or > 12)
            throw new DomainException($"Expiry month must be between 1 and 12. Received: {month}.");

        return new ExpiryDate(year, month);
    }

    /// <summary>Builds an expiry from any date, keeping only its year and month.</summary>
    public static ExpiryDate FromDate(DateOnly date) => new(date.Year, date.Month);

    public bool IsExpired(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow);
        return today > LastValidDay;
    }

    /// <summary>
    /// Classifies this expiry against the current instant. An item is <see cref="ExpiryStatus.ExpiringSoon"/>
    /// when it is not yet expired but its last valid day falls within <paramref name="warningWindow"/> from today.
    /// </summary>
    public ExpiryStatus GetStatus(IClock clock, TimeSpan warningWindow)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (warningWindow < TimeSpan.Zero)
            throw new DomainException("Warning window cannot be negative.");

        DateOnly today = DateOnly.FromDateTime(clock.UtcNow);

        if (today > LastValidDay)
            return ExpiryStatus.Expired;

        DateOnly warningThreshold = today.AddDays((int)warningWindow.TotalDays);
        return LastValidDay <= warningThreshold ? ExpiryStatus.ExpiringSoon : ExpiryStatus.Ok;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Year;
        yield return Month;
    }

    public override string ToString() => $"{Month:D2}/{Year}";
}
