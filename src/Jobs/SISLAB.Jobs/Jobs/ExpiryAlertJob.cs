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
/// Validity/expiry alert job (card [E6] #41). On each tick (daily by default) it scans every active company
/// for stock items that are expiring within the configured windows (30/15/7 days) or already expired, and
/// raises one in-app notification per at-risk item — deduplicated per day. Validity never blocks operation
/// (#47); this job only alerts.
/// </summary>
/// <remarks>
/// <para>
/// It reuses the E4 read query <see cref="ListExpiringItemsQuery"/> <b>intact</b>: the query reads the active
/// company from the effective <c>ITenantContext</c>, which the base (<see cref="CompanyScanAlertJob"/>) sets
/// per company via the tenant-override seam — so no company id flows through the query signature. One scan
/// runs with the widest window and <c>IncludeExpired = true</c>; <see cref="ExpiryAlertPolicy"/> then bands
/// each returned item into its severity (expired → Critical, ≤7d → Warning, ≤15/30d → Info).
/// </para>
/// <para>
/// <b>Idempotency</b> is delegated to the publisher via the day-bucketed dedupe key
/// (<c>expiry:stock_item:{id}:{yyyy-MM-dd}</c>): re-running the tick within the same day is a no-op, while a
/// new day legitimately re-alerts an item that is still at risk.
/// </para>
/// </remarks>
public sealed class ExpiryAlertJob : CompanyScanAlertJob
{
    private readonly ExpiryAlertOptions _options;
    private readonly IClock _clock;

    public ExpiryAlertJob(
        IServiceScopeFactory scopeFactory,
        IOptions<JobsOptions> options,
        IClock clock,
        ILogger<ExpiryAlertJob> logger)
        : base(scopeFactory, logger)
    {
        _options = options.Value.ExpiryAlert;
        _clock = clock;
    }

    /// <inheritdoc />
    protected override TimeSpan Interval => _options.Interval;

    /// <inheritdoc />
    protected override string BypassReason => "expiry-alert-scan";

    /// <summary>The widest configured warning window (defaults to 30) — the single window the scan requests.</summary>
    private int WidestWindowDays => _options.WindowDays.Count > 0 ? _options.WindowDays.Max() : 30;

    /// <inheritdoc />
    protected override async Task ScanCompanyAsync(
        IServiceScope companyScope,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        IMediator mediator = companyScope.ServiceProvider.GetRequiredService<IMediator>();
        INotificationPublisher publisher = companyScope.ServiceProvider.GetRequiredService<INotificationPublisher>();

        DateOnly scanDay = DateOnly.FromDateTime(_clock.UtcNow);

        IReadOnlyList<ExpiringItem> atRiskItems = await PagedQueryDrainer.DrainAsync(
            queryForPage: page => new ListExpiringItemsQuery
            {
                WarningWindowDays = WidestWindowDays,
                IncludeExpired = true,
                Page = page,
                PageSize = PagedQueryDrainer.PageSize
            },
            send: (query, ct) => mediator.SendAsync(query, ct),
            cancellationToken);

        foreach (ExpiringItem item in atRiskItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RaiseNotificationRequest request = ExpiryAlertPolicy.ToNotification(item, scanDay);
            await publisher.RaiseAsync(request, cancellationToken);
        }
    }
}
