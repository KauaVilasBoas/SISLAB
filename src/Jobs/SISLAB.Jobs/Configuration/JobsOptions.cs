namespace SISLAB.Jobs.Configuration;

/// <summary>
/// Strongly-typed configuration for the background jobs (E6 #39), bound from the <c>Jobs</c> section
/// of <c>appsettings</c> (see <c>AddSislabJobs</c>). Every value has a safe default so the worker runs
/// even when the section is absent; environments override only what they need.
///
/// Intervals are expressed as <see cref="TimeSpan"/> and parse from the standard <c>"hh:mm:ss"</c>
/// config format (e.g. <c>"00:00:30"</c> = 30s).
///
/// The Outbox dispatcher runs near-real-time; the three alert jobs (#41/#42/#66) default to a daily
/// cadence (they surface slow-moving, day-granularity conditions) but every interval is configurable.
/// </summary>
public sealed class JobsOptions
{
    /// <summary>Configuration section name (<c>appsettings.json</c> → <c>"Jobs"</c>).</summary>
    public const string SectionName = "Jobs";

    /// <summary>Settings for the Outbox dispatcher job (the E6 #39 example job / #40).</summary>
    public OutboxDispatcherOptions OutboxDispatcher { get; init; } = new();

    /// <summary>Settings for the validity/expiry alert job (#41).</summary>
    public ExpiryAlertOptions ExpiryAlert { get; init; } = new();

    /// <summary>Settings for the low-stock/reposition alert job (#42).</summary>
    public LowStockAlertOptions LowStockAlert { get; init; } = new();

    /// <summary>Settings for the overdue-calibration alert job (#66).</summary>
    public CalibrationAlertOptions CalibrationAlert { get; init; } = new();

    /// <summary>Settings for the controlled-compliance alert job (#108).</summary>
    public ControlledComplianceAlertOptions ControlledComplianceAlert { get; init; } = new();

    /// <summary>Settings for the presentation 15-day reminder job (E6 #83).</summary>
    public PresentationReminderOptions PresentationReminder { get; init; } = new();

    /// <summary>Settings for the biotério weekly reminder job (E6 #83).</summary>
    public BioteriumReminderOptions BioteriumReminder { get; init; } = new();

    /// <summary>Settings for the configurable calendar-entry reminder job (E10.8 #5).</summary>
    public AgendaReminderOptions AgendaReminder { get; init; } = new();
}

/// <summary>
/// Settings for the validity/expiry alert job (#41): how often it runs and the warning windows (in days)
/// it scans. Each window becomes a severity band — the tightest matching window wins — so an item is
/// alerted once per cycle at its most urgent applicable level. The default cadence is daily and the default
/// windows are the prototype's 30/15/7 days; already-expired items are always included (they are Critical).
/// </summary>
public sealed class ExpiryAlertOptions
{
    /// <summary>How often the expiry scan runs. Default: once a day.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Warning windows in days, from widest to tightest (default 30/15/7). An item entering the widest
    /// window is alerted; the tightest window it also falls within decides its severity. Order is
    /// normalized (descending) by the job, so configuration may list them in any order.
    /// </summary>
    public IReadOnlyList<int> WindowDays { get; init; } = [30, 15, 7];
}

/// <summary>
/// Settings for the low-stock/reposition alert job (#42): how often it runs. It complements the real-time
/// <c>StockBelowMinimum</c> event with a periodic safety sweep. Default cadence is daily.
/// </summary>
public sealed class LowStockAlertOptions
{
    /// <summary>How often the low-stock sweep runs. Default: once a day.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromDays(1);
}

/// <summary>
/// Settings for the overdue-calibration alert job (#66): how often it runs. It scans equipment whose next
/// calibration date has already passed (calibration not applicable is ignored). Default cadence is daily.
/// </summary>
public sealed class CalibrationAlertOptions
{
    /// <summary>How often the calibration scan runs. Default: once a day.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromDays(1);
}

/// <summary>
/// Settings for the controlled-compliance alert job (#108): how often it runs and the warning window (in days).
/// Defaulting to daily with a 30-day window so near-expiry controlled substances are caught early.
/// </summary>
public sealed class ControlledComplianceAlertOptions
{
    /// <summary>How often the compliance scan runs. Default: once a day.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Warning window in days: controlled items expiring within this window are raised as Warning;
    /// already-expired items are always raised as Critical. Default: 30 days.
    /// </summary>
    public int WindowDays { get; init; } = 30;
}

/// <summary>Settings for the presentation 15-day reminder job (E6 #83).</summary>
public sealed class PresentationReminderOptions
{
    /// <summary>How often the job runs. Default: once a day.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromDays(1);

    /// <summary>Days in advance the reminder is sent. Default: 15 days.</summary>
    public int WindowDays { get; init; } = 15;
}

/// <summary>Settings for the biotério weekly reminder job (E6 #83).</summary>
public sealed class BioteriumReminderOptions
{
    /// <summary>How often the job runs. Default: daily (it self-limits to Mondays).</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromDays(1);
}

/// <summary>
/// Settings for the configurable calendar-entry reminder job (card [E10.8] #5). The job runs frequently and
/// fires each entry's reminders <c>MinutesBefore</c> the occurrence start. <see cref="Interval"/> doubles as the
/// firing tolerance: a reminder whose fire-time fell within the last interval is raised now, so no reminder is
/// missed between ticks and none double-fires (backed by an occurrence-bucketed dedupe key).
/// </summary>
public sealed class AgendaReminderOptions
{
    /// <summary>How often the reminder scan runs (and the fire-time tolerance window). Default: 5 minutes.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How far ahead to expand recurring series when hunting for upcoming occurrences. Bounds the expansion cost;
    /// must exceed the largest configured reminder lead time. Default: 30 days.
    /// </summary>
    public TimeSpan LookAhead { get; init; } = TimeSpan.FromDays(30);
}

/// <summary>
/// Settings for the Outbox dispatcher job: how often it runs, how many pending messages it drains per
/// tick, and how many times a failing message is retried before being dead-lettered. The defaults
/// (5s cadence, 50 messages, 5 attempts) keep integration events near-real-time without hammering the
/// database and stop a poison message from blocking the loop forever.
/// </summary>
public sealed class OutboxDispatcherOptions
{
    /// <summary>How often the Outbox is scanned for pending messages. Default: 5 seconds.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum pending messages published per tick. Default: 50.</summary>
    public int BatchSize { get; init; } = 50;

    /// <summary>
    /// How many failed delivery attempts a message tolerates before it is dead-lettered and no longer
    /// retried. Default: 5.
    /// </summary>
    public int MaxAttempts { get; init; } = 5;
}
