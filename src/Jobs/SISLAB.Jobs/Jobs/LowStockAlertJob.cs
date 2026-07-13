using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SISLAB.Jobs.Configuration;
using SISLAB.Modules.Inventory.Application.StockRead;
using SISLAB.Modules.Notifications.Contracts;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Jobs.Jobs;

/// <summary>
/// Low-stock/reposition alert job (card [E6] #42). On each tick (daily by default) it scans every active
/// company for stock items below their configured minimum and raises one in-app notification per item —
/// deduplicated per day. It is the periodic safety sweep that complements the real-time
/// <c>StockBelowMinimum</c> domain event fired on consumption, catching items that slipped below minimum by
/// other means (edits, corrections) without the two paths conflicting (the dedupe key makes re-alerts idempotent).
/// </summary>
/// <remarks>
/// It reuses the E4 read query <see cref="ListItemsBelowMinimumQuery"/> <b>intact</b>, tenant-scoped by the
/// override seam the base sets per company. <see cref="LowStockAlertPolicy"/> bands severity (controlled →
/// Critical, otherwise Warning) and builds the day-bucketed dedupe key
/// (<c>lowstock:stock_item:{id}:{yyyy-MM-dd}</c>).
/// </remarks>
public sealed class LowStockAlertJob : CompanyScanAlertJob
{
    private readonly LowStockAlertOptions _options;
    private readonly ILogger<LowStockAlertJob> _logger;

    public LowStockAlertJob(
        IServiceScopeFactory scopeFactory,
        IOptions<JobsOptions> options,
        IClock clock,
        ILogger<LowStockAlertJob> logger)
        : base(scopeFactory, clock, logger)
    {
        _options = options.Value.LowStockAlert;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override TimeSpan Interval => _options.Interval;

    /// <inheritdoc />
    protected override string BypassReason => "low-stock-alert-scan";

    /// <inheritdoc />
    protected override async Task ScanCompanyAsync(
        IServiceScope companyScope,
        Guid companyId,
        DateOnly scanDay,
        CancellationToken cancellationToken)
    {
        IMediator mediator = companyScope.ServiceProvider.GetRequiredService<IMediator>();
        INotificationPublisher publisher = companyScope.ServiceProvider.GetRequiredService<INotificationPublisher>();

        IReadOnlyList<BelowMinimumItem> belowMinimumItems = await PagedQueryDrainer.DrainAsync(
            mediator,
            queryForPage: page => new ListItemsBelowMinimumQuery
            {
                Page = page,
                PageSize = ScanPageSize
            },
            cancellationToken);

        foreach (BelowMinimumItem item in belowMinimumItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                RaiseNotificationRequest request = LowStockAlertPolicy.ToNotification(item, scanDay);
                await publisher.RaiseAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One item's failure must not skip the remaining below-minimum items for this company.
                _logger.LogError(ex,
                    "Low-stock alert job failed to raise notification for item {ItemId} in company {CompanyId}; skipping to the next item.",
                    item.Id, companyId);
            }
        }
    }
}
