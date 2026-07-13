using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Notifications.Tests.TestSupport;

/// <summary>A clock frozen at a fixed instant, so aggregate timestamps are deterministic in tests.</summary>
internal sealed class FixedClock : IClock
{
    public FixedClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; }
}
