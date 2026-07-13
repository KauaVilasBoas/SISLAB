using SISLAB.Modules.Inventory.Application.StockMovements.Queries;
using SISLAB.Modules.Notifications.Contracts;

namespace SISLAB.Jobs.Jobs;

/// <summary>
/// Pure policy that turns a <see cref="BelowMinimumItem"/> into its reposition notification and per-cycle
/// dedupe key (#42). Clock- and I/O-free so severity and the key are unit-testable in isolation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Severity.</b> A below-minimum item is <see cref="NotificationSeverityLevel.Warning"/> (reposition is
/// advisable), escalated to <see cref="NotificationSeverityLevel.Critical"/> when the item is controlled —
/// running a controlled substance below its minimum is a compliance concern, consistent with how the lab
/// treats controlled items elsewhere.
/// </para>
/// <para>
/// <b>Dedupe key</b> is <c>lowstock:stock_item:{id}:{yyyy-MM-dd}</c>: one alert per item per scan day. The
/// item stays deduplicated across a day's ticks while still below minimum, and re-alerts on the next day.
/// </para>
/// </remarks>
internal static class LowStockAlertPolicy
{
    internal const string TargetType = "stock_item";

    internal static RaiseNotificationRequest ToNotification(BelowMinimumItem item, DateOnly scanDay)
    {
        string location = string.IsNullOrWhiteSpace(item.StorageLocationName)
            ? "local não informado"
            : item.StorageLocationName;

        string description =
            $"Saldo {item.Quantity:0.##} {item.Unit} abaixo do mínimo " +
            $"{item.MinimumQuantity:0.##} {item.MinimumUnit} (faltam {item.Deficit:0.##} {item.MinimumUnit}) — {location}.";

        return new RaiseNotificationRequest(
            Type: NotificationTypeCode.LowStock,
            Severity: ClassifySeverity(item),
            Title: $"{item.Name}: estoque baixo",
            Description: description,
            TargetType: TargetType,
            TargetId: item.Id,
            DedupeKey: BuildDedupeKey(item.Id, scanDay));
    }

    /// <summary>Controlled items below minimum are Critical (compliance); others are Warning.</summary>
    internal static NotificationSeverityLevel ClassifySeverity(BelowMinimumItem item)
        => item.IsControlled ? NotificationSeverityLevel.Critical : NotificationSeverityLevel.Warning;

    /// <summary>Composes the per-cycle idempotency key: <c>lowstock:stock_item:{id}:{yyyy-MM-dd}</c>.</summary>
    internal static string BuildDedupeKey(Guid itemId, DateOnly scanDay)
        => $"lowstock:{TargetType}:{itemId}:{scanDay:yyyy-MM-dd}";
}
