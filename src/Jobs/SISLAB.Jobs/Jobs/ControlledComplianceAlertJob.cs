using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SISLAB.Jobs.Configuration;
using SISLAB.Modules.Inventory.Application.StockMovements.Queries;
using SISLAB.Modules.Notifications.Contracts;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Jobs.Jobs;

/// <summary>
/// Controlled-compliance alert job (card [E7] #108). On each tick (daily by default) it scans every active
/// company for <b>controlled substances</b> that are expired or expiring within the configured window, and
/// raises one <see cref="SISLAB.Modules.Notifications.Contracts.NotificationTypeCode.ControlledCompliance"/>
/// in-app notification per at-risk controlled item — deduplicated per day.
/// </summary>
/// <remarks>
/// <para>
/// Reuses <see cref="ListExpiringItemsQuery"/> (the same query the expiry-alert job uses for all items) and
/// filters the result to those where <see cref="ExpiringItem.IsControlled"/> is true. The scan intentionally
/// runs as a separate job so compliance alerts have a distinct <c>NotificationType</c> and can be displayed,
/// filtered and acted on independently of the general expiry alerts (card #108 rationale).
/// </para>
/// <para>
/// <b>Idempotency</b> is delegated to the publisher via the day-bucketed dedupe key
/// (<c>controlled:stock_item:{id}:{yyyy-MM-dd}</c>): re-running the tick within the same day is a no-op,
/// while a new day legitimately re-alerts an item that is still at risk.
/// </para>
/// </remarks>
public sealed class ControlledComplianceAlertJob : CompanyScanAlertJob
{
    private readonly ControlledComplianceAlertOptions _options;
    private readonly ILogger<ControlledComplianceAlertJob> _logger;

    public ControlledComplianceAlertJob(
        IServiceScopeFactory scopeFactory,
        IOptions<JobsOptions> options,
        IClock clock,
        ILogger<ControlledComplianceAlertJob> logger)
        : base(scopeFactory, clock, logger)
    {
        _options = options.Value.ControlledComplianceAlert;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override TimeSpan Interval => _options.Interval;

    /// <inheritdoc />
    protected override string BypassReason => "controlled-compliance-scan";

    /// <inheritdoc />
    protected override async Task ScanCompanyAsync(
        IServiceScope companyScope,
        Guid companyId,
        DateOnly scanDay,
        CancellationToken cancellationToken)
    {
        IMediator mediator = companyScope.ServiceProvider.GetRequiredService<IMediator>();
        INotificationPublisher publisher = companyScope.ServiceProvider.GetRequiredService<INotificationPublisher>();

        // Drain the at-risk CONTROLLED items only (the filter is pushed into SQL, not applied in memory),
        // using the configured window so we catch approaching-expiry controlled items too.
        IReadOnlyList<ExpiringItem> atRiskItems = await PagedQueryDrainer.DrainAsync(
            mediator,
            queryForPage: page => new ListExpiringItemsQuery
            {
                WarningWindowDays = _options.WindowDays,
                IncludeExpired = true,
                ControlledOnly = true,
                Page = page,
                PageSize = ScanPageSize
            },
            cancellationToken);

        foreach (ExpiringItem item in atRiskItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                RaiseNotificationRequest request = ControlledComplianceAlertPolicy.ToNotification(item, scanDay);
                await publisher.RaiseAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Controlled compliance alert job failed to raise notification for item {ItemId} in company {CompanyId}; skipping to the next item.",
                    item.Id, companyId);
            }
        }
    }
}
