using SISLAB.Modules.Inventory.Application.EquipmentRead;
using SISLAB.Modules.Notifications.Contracts;

namespace SISLAB.Jobs.Jobs;

/// <summary>
/// Pure policy that turns an <see cref="OverdueCalibrationEquipment"/> into its calibration notification and
/// per-cycle dedupe key (#66). Clock- and I/O-free so the mapping is unit-testable in isolation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Severity.</b> An equipment with overdue calibration is <see cref="NotificationSeverityLevel.Warning"/>
/// — action is advisable but calibration lateness does not block operating the equipment, consistent with the
/// alerts-only stance of the E6 jobs.
/// </para>
/// <para>
/// <b>Dedupe key</b> is <c>calibration:equipment:{id}:{yyyy-MM-dd}</c>: one alert per equipment per scan day,
/// re-firing daily while it stays overdue without duplicating within a day's cycle.
/// </para>
/// </remarks>
internal static class CalibrationAlertPolicy
{
    internal const string TargetType = "equipment";

    internal static RaiseNotificationRequest ToNotification(OverdueCalibrationEquipment equipment, DateOnly scanDay)
    {
        string when = equipment.DaysOverdue == 1
            ? "há 1 dia"
            : $"há {equipment.DaysOverdue} dias";

        string description =
            $"Calibração vencida {when} (prevista para {equipment.NextCalibration:dd/MM/yyyy}) — patrimônio {equipment.AssetTag}.";

        return new RaiseNotificationRequest(
            Type: NotificationTypeCode.Calibration,
            Severity: NotificationSeverityLevel.Warning,
            Title: $"{equipment.Name}: calibração atrasada",
            Description: description,
            TargetType: TargetType,
            TargetId: equipment.Id,
            DedupeKey: BuildDedupeKey(equipment.Id, scanDay));
    }

    /// <summary>Composes the per-cycle idempotency key: <c>calibration:equipment:{id}:{yyyy-MM-dd}</c>.</summary>
    internal static string BuildDedupeKey(Guid equipmentId, DateOnly scanDay)
        => $"calibration:{TargetType}:{equipmentId}:{scanDay:yyyy-MM-dd}";
}
