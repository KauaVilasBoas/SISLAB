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
    private readonly ILogger<CalibrationOverdueAlertJob> _logger;

    public CalibrationOverdueAlertJob(
        IServiceScopeFactory scopeFactory,
        IOptions<JobsOptions> options,
        IClock clock,
        ILogger<CalibrationOverdueAlertJob> logger)
        : base(scopeFactory, clock, logger)
    {
        _options = options.Value.CalibrationAlert;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override TimeSpan Interval => _options.Interval;

    /// <inheritdoc />
    protected override string BypassReason => "calibration-alert-scan";

    /// <inheritdoc />
    protected override async Task ScanCompanyAsync(
        IServiceScope companyScope,
        Guid companyId,
        DateOnly scanDay,
        CancellationToken cancellationToken)
    {
        IMediator mediator = companyScope.ServiceProvider.GetRequiredService<IMediator>();
        INotificationPublisher publisher = companyScope.ServiceProvider.GetRequiredService<INotificationPublisher>();

        IReadOnlyList<OverdueCalibrationEquipment> overdueEquipment = await PagedQueryDrainer.DrainAsync(
            mediator,
            queryForPage: page => new ListOverdueCalibrationEquipmentQuery
            {
                Page = page,
                PageSize = ScanPageSize
            },
            cancellationToken);

        foreach (OverdueCalibrationEquipment equipment in overdueEquipment)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                RaiseNotificationRequest request = CalibrationAlertPolicy.ToNotification(equipment, scanDay);
                await publisher.RaiseAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One equipment's failure must not skip the remaining overdue equipment for this company.
                _logger.LogError(ex,
                    "Calibration alert job failed to raise notification for equipment {EquipmentId} in company {CompanyId}; skipping to the next equipment.",
                    equipment.Id, companyId);
            }
        }
    }
}
