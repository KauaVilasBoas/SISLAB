using SISLAB.Modules.Inventory.Application.StockMovements.Queries;
using SISLAB.Modules.Notifications.Contracts;

namespace SISLAB.Jobs.Jobs;

/// <summary>
/// Pure policy that turns an at-risk <see cref="ExpiringItem"/> into the notification an operator sees
/// (severity, title, description) and its per-cycle dedupe key (#41). Kept clock- and I/O-free so the
/// severity banding and the dedupe key are unit-testable without a database or a running host.
/// </summary>
/// <remarks>
/// <para>
/// <b>Severity banding</b> (project decision): an already-expired item is <see cref="NotificationSeverityLevel.Critical"/>
/// (compliance risk — but note validity never blocks operation, #47); an item within 7 days is
/// <see cref="NotificationSeverityLevel.Warning"/>; anything else still inside the widest window (15/30 days)
/// is <see cref="NotificationSeverityLevel.Info"/>. <c>DaysRemaining</c> is signed (negative once expired),
/// so the bands read directly off it, with <see cref="ExpiryStatusView.Expired"/> as the authoritative
/// "already expired" signal.
/// </para>
/// <para>
/// <b>Dedupe key</b> is <c>expiry:stock_item:{id}:{bucket}</c> where the bucket is the scan day
/// (<c>yyyy-MM-dd</c>): the alert re-fires each day the item stays at risk but never duplicates within one
/// day's cycle, and the id keeps it per-item.
/// </para>
/// </remarks>
internal static class ExpiryAlertPolicy
{
    internal const string TargetType = "stock_item";

    /// <summary>Number of days at or below which an approaching-expiry item is escalated to Warning.</summary>
    internal const int WarningBandDays = 7;

    /// <summary>Builds the notification request for an at-risk item on the given scan day.</summary>
    internal static RaiseNotificationRequest ToNotification(ExpiringItem item, DateOnly scanDay)
    {
        bool expired = item.ExpiryStatus == ExpiryStatusView.Expired || item.DaysRemaining < 0;
        NotificationSeverityLevel severity = ClassifySeverity(item);

        string title = expired
            ? $"{item.Name}: validade vencida"
            : $"{item.Name}: validade próxima";

        string description = BuildDescription(item, expired);

        return new RaiseNotificationRequest(
            Type: NotificationTypeCode.Expiry,
            Severity: severity,
            Title: title,
            Description: description,
            TargetType: TargetType,
            TargetId: item.Id,
            DedupeKey: BuildDedupeKey(item.Id, scanDay));
    }

    /// <summary>Maps an at-risk item to its severity band (Critical / Warning / Info).</summary>
    internal static NotificationSeverityLevel ClassifySeverity(ExpiringItem item)
    {
        if (item.ExpiryStatus == ExpiryStatusView.Expired || item.DaysRemaining < 0)
            return NotificationSeverityLevel.Critical;

        return item.DaysRemaining <= WarningBandDays
            ? NotificationSeverityLevel.Warning
            : NotificationSeverityLevel.Info;
    }

    /// <summary>Composes the per-cycle idempotency key: <c>expiry:stock_item:{id}:{yyyy-MM-dd}</c>.</summary>
    internal static string BuildDedupeKey(Guid itemId, DateOnly scanDay)
        => $"expiry:{TargetType}:{itemId}:{scanDay:yyyy-MM-dd}";

    private static string BuildDescription(ExpiringItem item, bool expired)
    {
        string location = string.IsNullOrWhiteSpace(item.StorageLocationName)
            ? "local não informado"
            : item.StorageLocationName;

        if (expired)
        {
            int daysAgo = Math.Abs(item.DaysRemaining);
            string when = daysAgo == 0 ? "hoje" : $"há {daysAgo} dia(s)";
            return $"Vencido {when} — {location}.";
        }

        string dueIn = item.DaysRemaining == 0 ? "hoje" : $"em {item.DaysRemaining} dia(s)";
        return $"Vence {dueIn} — {location}.";
    }
}
