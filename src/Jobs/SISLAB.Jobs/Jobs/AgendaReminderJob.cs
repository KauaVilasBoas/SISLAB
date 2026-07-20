using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SISLAB.Jobs.Configuration;
using SISLAB.Modules.Agenda.Application.Entries.Queries;
using SISLAB.Modules.Agenda.Application.Entries.Recurrence;
using SISLAB.Modules.Notifications.Contracts;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Jobs.Jobs;

/// <summary>
/// Configurable calendar-entry reminder job (card [E10.8] #5). On each tick it scans every active company for
/// entries that carry reminders, expands their (possibly recurring) occurrences within the look-ahead window,
/// and raises one in-app <see cref="NotificationTypeCode.AgendaReminder"/> per reminder whose fire-time
/// (<c>occurrenceStart − minutesBefore</c>) fell within the last tick interval — so a reminder fires once, close
/// to its lead time, without being missed between ticks.
/// </summary>
/// <remarks>
/// Idempotency rides on the notification's dedupe key, bucketed by entry + occurrence date + lead time, exactly
/// like the presentation/biotério reminders: a re-run in the same interval is a no-op, and a later occurrence of
/// a recurring entry legitimately re-fires under a new key.
/// </remarks>
public sealed class AgendaReminderJob : CompanyScanAlertJob
{
    private readonly AgendaReminderOptions _options;
    private readonly IClock _clock;
    private readonly RecurrenceExpander _expander;
    private readonly ILogger<AgendaReminderJob> _logger;

    public AgendaReminderJob(
        IServiceScopeFactory scopeFactory,
        IOptions<JobsOptions> options,
        IClock clock,
        RecurrenceExpander expander,
        ILogger<AgendaReminderJob> logger)
        : base(scopeFactory, clock, logger)
    {
        _options = options.Value.AgendaReminder;
        _clock = clock;
        _expander = expander;
        _logger = logger;
    }

    protected override TimeSpan Interval => _options.Interval;
    protected override string BypassReason => "agenda-reminder-scan";

    protected override async Task ScanCompanyAsync(
        IServiceScope companyScope,
        Guid companyId,
        DateOnly scanDay,
        CancellationToken cancellationToken)
    {
        DateTime nowUtc = _clock.UtcNow;
        DateTime windowFloorUtc = nowUtc - _options.Interval; // reminders whose fire-time is in (floor, now] are due
        DateTime horizonUtc = nowUtc + _options.LookAhead;

        IMediator mediator = companyScope.ServiceProvider.GetRequiredService<IMediator>();
        INotificationPublisher publisher = companyScope.ServiceProvider.GetRequiredService<INotificationPublisher>();

        IReadOnlyList<ReminderCandidate> candidates = await mediator.SendAsync(
            new GetEntriesWithRemindersQuery(horizonUtc),
            cancellationToken);

        foreach (ReminderCandidate candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await FireDueRemindersAsync(candidate, nowUtc, windowFloorUtc, horizonUtc, publisher, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Agenda reminder job failed for entry {EntryId} in company {CompanyId}; skipping.",
                    candidate.EntryId, companyId);
            }
        }
    }

    private async Task FireDueRemindersAsync(
        ReminderCandidate candidate,
        DateTime nowUtc,
        DateTime windowFloorUtc,
        DateTime horizonUtc,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<EntryOccurrence> occurrences = _expander.Expand(
            candidate.StartDateUtc, candidate.EndDateUtc, candidate.RecurrenceRule,
            candidate.ExcludedDates, windowStartUtc: windowFloorUtc, windowEndUtc: horizonUtc);

        foreach (EntryOccurrence occurrence in occurrences)
        {
            foreach (ReminderCandidateReminder reminder in candidate.Reminders)
            {
                DateTime fireAtUtc = occurrence.StartUtc.AddMinutes(-reminder.MinutesBefore);

                // Due iff the fire-time slid into the last interval — inclusive of now, exclusive of the floor,
                // so a reminder is picked up exactly once across consecutive ticks.
                if (fireAtUtc <= nowUtc && fireAtUtc > windowFloorUtc)
                    await RaiseAsync(candidate, occurrence, reminder, publisher, cancellationToken);
            }
        }
    }

    private static Task RaiseAsync(
        ReminderCandidate candidate,
        EntryOccurrence occurrence,
        ReminderCandidateReminder reminder,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
        => publisher.RaiseAsync(new RaiseNotificationRequest(
            Type: NotificationTypeCode.AgendaReminder,
            Severity: NotificationSeverityLevel.Info,
            Title: $"Lembrete: {candidate.Title}",
            Description: $"Começa em {reminder.MinutesBefore} min ({occurrence.StartUtc:dd/MM HH:mm} UTC).",
            TargetType: "agenda_entry",
            TargetId: candidate.EntryId,
            // Bucket by entry + occurrence date + lead time so each reminder fires once per occurrence.
            DedupeKey: $"agenda-reminder:{candidate.EntryId}:{occurrence.OccurrenceDate:yyyy-MM-dd}:{reminder.MinutesBefore}"),
            cancellationToken);
}
