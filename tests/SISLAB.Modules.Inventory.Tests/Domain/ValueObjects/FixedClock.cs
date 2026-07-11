using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Tests.Domain.ValueObjects;

/// <summary>Test double for <see cref="IClock"/> returning a fixed instant.</summary>
internal sealed class FixedClock : IClock
{
    public FixedClock(DateTime utcNow) => UtcNow = utcNow;

    public static FixedClock On(int year, int month, int day) =>
        new(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc));

    public DateTime UtcNow { get; }
}
