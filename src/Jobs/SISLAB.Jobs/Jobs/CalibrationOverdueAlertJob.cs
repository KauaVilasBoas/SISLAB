using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SISLAB.Jobs.Configuration;
using SISLAB.Modules.Inventory.Application.EquipmentRead;
using SISLAB.Modules.Notifications.Contracts;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Jobs.Jobs;

/// <summary>
/// Overdue-calibration alert job (card [E6] #66). On each tick (daily by default) it scans every active
/// company for equipment whose next calibration date has already passed and raises one in-app notification
/// per equipment — deduplicated per day. Equipment with no planned next calibration (n/a) is ignored by the
/// query, so no alert is raised for it. Analogous to the validity (#41) and low-stock (#42) jobs.
/// </summary>
/// <remarks>
/// It runs the new E6 read query <see cref="ListOverdueCalibrationEquipmentQuery"/> (status derived in SQL,
/// tenant-scoped by the override seam the base sets per company). <see cref="CalibrationAlertPolicy"/> maps
/// each equipment to its Warning notification and the day-bucketed dedupe key
/// (<c>calibration:equipment:{id}:{yyyy-MM-dd}</c>).
/// </remarks>
public sealed class CalibrationOverdueAlertJob : CompanyScanAlertJob
{
    private readonly CalibrationAlertOptions _options;
    private readonly IClock _clock;

    public CalibrationOverdueAlertJob(
        IServiceScopeFactory scopeFactory,
        IOptions<JobsOptions> options,
        IClock clock,
        ILogger<CalibrationOverdueAlertJob> logger)
        : base(scopeFactory, logger)
    {
        _options = options.Value.CalibrationAlert;
        _clock = clock;
    }

    /// <inheritdoc />
    protected override TimeSpan Interval => _options.Interval;

    /// <inheritdoc />
    protected override string BypassReason => "calibration-alert-scan";

    /// <inheritdoc />
    protected override async Task ScanCompanyAsync(
        IServiceScope companyScope,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        IMediator mediator = companyScope.ServiceProvider.GetRequiredService<IMediator>();
        INotificationPublisher publisher = companyScope.ServiceProvider.GetRequiredService<INotificationPublisher>();

        DateOnly scanDay = DateOnly.FromDateTime(_clock.UtcNow);

        IReadOnlyList<OverdueCalibrationEquipment> overdueEquipment = await PagedQueryDrainer.DrainAsync(
            queryForPage: page => new ListOverdueCalibrationEquipmentQuery
            {
                Page = page,
                PageSize = PagedQueryDrainer.PageSize
            },
            send: (query, ct) => mediator.SendAsync(query, ct),
            cancellationToken);

        foreach (OverdueCalibrationEquipment equipment in overdueEquipment)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RaiseNotificationRequest request = CalibrationAlertPolicy.ToNotification(equipment, scanDay);
            await publisher.RaiseAsync(request, cancellationToken);
        }
    }
}
