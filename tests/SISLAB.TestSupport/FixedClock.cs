using SISLAB.SharedKernel.Time;

namespace SISLAB.TestSupport;

/// <summary>
/// An <see cref="IClock"/> frozen at a fixed instant, so timestamps are deterministic in tests. Shared by
/// every test project instead of being re-declared per assembly.
/// </summary>
public sealed class FixedClock : IClock
{
    public FixedClock(DateTime utcNow) => UtcNow = utcNow;

    /// <summary>Convenience factory for a UTC midnight on the given date.</summary>
    public static FixedClock On(int year, int month, int day) =>
        new(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc));

    public DateTime UtcNow { get; }
}
