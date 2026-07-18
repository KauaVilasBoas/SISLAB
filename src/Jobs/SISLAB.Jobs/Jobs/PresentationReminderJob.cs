using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SISLAB.Jobs.Configuration;
using SISLAB.Modules.Agenda.Application.Presentations.Commands;
using SISLAB.Modules.Agenda.Application.Presentations.Queries;
using SISLAB.Modules.Notifications.Contracts;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Jobs.Jobs;

/// <summary>
/// Presentation 15-day reminder job (card [E6] #83). On each daily tick it scans every active company
/// for presentations scheduled within the configured window (default 15 days) that have not yet had a
/// reminder notification sent, raises one in-app <see cref="NotificationTypeCode.PresentationReminder"/>
/// per qualifying presentation, and marks <c>ReminderSentAt</c> on the aggregate so the reminder fires
/// only once.
/// </summary>
public sealed class PresentationReminderJob : CompanyScanAlertJob
{
    private readonly PresentationReminderOptions _options;
    private readonly ILogger<PresentationReminderJob> _logger;

    public PresentationReminderJob(
        IServiceScopeFactory scopeFactory,
        IOptions<JobsOptions> options,
        IClock clock,
        ILogger<PresentationReminderJob> logger)
        : base(scopeFactory, clock, logger)
    {
        _options = options.Value.PresentationReminder;
        _logger = logger;
    }

    protected override TimeSpan Interval => _options.Interval;
    protected override string BypassReason => "presentation-reminder-scan";

    protected override async Task ScanCompanyAsync(
        IServiceScope companyScope,
        Guid companyId,
        DateOnly scanDay,
        CancellationToken cancellationToken)
    {
        IMediator mediator = companyScope.ServiceProvider.GetRequiredService<IMediator>();
        INotificationPublisher publisher = companyScope.ServiceProvider.GetRequiredService<INotificationPublisher>();

        IReadOnlyList<PresentationReminderItem> upcoming = await mediator.SendAsync(
            new GetPresentationsForReminderQuery(scanDay, _options.WindowDays),
            cancellationToken);

        foreach (PresentationReminderItem presentation in upcoming)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                int daysUntil = presentation.ScheduledDate.DayNumber - scanDay.DayNumber;

                await publisher.RaiseAsync(new RaiseNotificationRequest(
                    Type: NotificationTypeCode.PresentationReminder,
                    Severity: NotificationSeverityLevel.Info,
                    Title: $"Apresentação em {daysUntil} dia(s): {presentation.Title}",
                    Description: $"{presentation.PresenterName} deve enviar o material até {presentation.ScheduledDate.AddDays(-15):dd/MM}.",
                    TargetType: "presentation",
                    TargetId: presentation.Id,
                    DedupeKey: $"presentation-reminder:{presentation.Id}:{scanDay:yyyy-MM-dd}"),
                    cancellationToken);

                // Mark reminder sent so it never fires again for this presentation.
                await mediator.SendAsync(new MarkPresentationReminderSentCommand(presentation.Id), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Presentation reminder job failed for presentation {PresentationId} in company {CompanyId}; skipping.",
                    presentation.Id, companyId);
            }
        }
    }
}
