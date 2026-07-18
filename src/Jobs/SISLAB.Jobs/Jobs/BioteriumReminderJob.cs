using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SISLAB.Jobs.Configuration;
using SISLAB.Modules.Agenda.Application.Bioterium.Queries;
using SISLAB.Modules.Notifications.Contracts;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Jobs.Jobs;

/// <summary>
/// Biotério weekly reminder job (card [E6] #83). On each Monday tick it scans every active company for
/// pending biotério assignments in the current week (Mon + Thu) and raises one
/// <see cref="NotificationTypeCode.BioteriumReminder"/> in-app notification per assignment, reminding
/// the whole company who is responsible this week. Deduplicated per assignment per day so re-runs on the
/// same Monday are a no-op.
/// </summary>
public sealed class BioteriumReminderJob : CompanyScanAlertJob
{
    private readonly BioteriumReminderOptions _options;
    private readonly ILogger<BioteriumReminderJob> _logger;

    public BioteriumReminderJob(
        IServiceScopeFactory scopeFactory,
        IOptions<JobsOptions> options,
        IClock clock,
        ILogger<BioteriumReminderJob> logger)
        : base(scopeFactory, clock, logger)
    {
        _options = options.Value.BioteriumReminder;
        _logger = logger;
    }

    protected override TimeSpan Interval => _options.Interval;
    protected override string BypassReason => "bioterium-reminder-scan";

    protected override async Task ScanCompanyAsync(
        IServiceScope companyScope,
        Guid companyId,
        DateOnly scanDay,
        CancellationToken cancellationToken)
    {
        // Only run on Mondays — biotério assignments are Mon + Thu; we notify at the week's start.
        if (scanDay.DayOfWeek != DayOfWeek.Monday)
            return;

        DateOnly thursday = scanDay.AddDays(3);

        IMediator mediator = companyScope.ServiceProvider.GetRequiredService<IMediator>();
        INotificationPublisher publisher = companyScope.ServiceProvider.GetRequiredService<INotificationPublisher>();

        IReadOnlyList<BioteriumReminderItem> assignments = await mediator.SendAsync(
            new GetBioteriumForReminderQuery(scanDay, thursday),
            cancellationToken);

        foreach (BioteriumReminderItem assignment in assignments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                string day = assignment.AssignmentDate.DayOfWeek == DayOfWeek.Monday
                    ? "segunda-feira"
                    : "quinta-feira";

                await publisher.RaiseAsync(new RaiseNotificationRequest(
                    Type: NotificationTypeCode.BioteriumReminder,
                    Severity: NotificationSeverityLevel.Info,
                    Title: $"Troca do biotério — {day}: {assignment.ResponsibleName}",
                    Description: $"Limpeza das caixas agendada para {assignment.AssignmentDate:dd/MM}.",
                    TargetType: "bioterium_assignment",
                    TargetId: assignment.Id,
                    DedupeKey: $"bioterium-reminder:{assignment.Id}:{scanDay:yyyy-MM-dd}"),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Biotério reminder job failed for assignment {AssignmentId} in company {CompanyId}; skipping.",
                    assignment.Id, companyId);
            }
        }
    }
}
