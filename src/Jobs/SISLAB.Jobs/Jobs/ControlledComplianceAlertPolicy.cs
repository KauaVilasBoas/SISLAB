using SISLAB.Modules.Inventory.Application.StockMovements.Queries;
using SISLAB.Modules.Notifications.Contracts;

namespace SISLAB.Jobs.Jobs;

/// <summary>
/// Pure policy that turns an expired or near-expiry <b>controlled</b> substance into the compliance
/// notification operators see (card [E7] #108). Kept clock- and I/O-free so severity banding and dedupe
/// key are unit-testable without a database.
/// </summary>
/// <remarks>
/// <para>
/// Only expired items are raised as <see cref="NotificationSeverityLevel.Critical"/> compliance alerts;
/// items that are still within the warning window (≤7 days) are <see cref="NotificationSeverityLevel.Warning"/>.
/// The caller is responsible for filtering to <see cref="ExpiringItem.IsControlled"/> before invoking this policy.
/// </para>
/// <para>
/// <b>Dedupe key</b> is <c>controlled:stock_item:{id}:{yyyy-MM-dd}</c> — same day-bucket pattern as the expiry
/// job, so a rerun within the same day is a no-op and a new day legitimately re-alerts a still-at-risk item.
/// </para>
/// </remarks>
internal static class ControlledComplianceAlertPolicy
{
    internal const string TargetType = "stock_item";

    /// <summary>Builds the compliance notification request for a controlled item on the given scan day.</summary>
    internal static RaiseNotificationRequest ToNotification(ExpiringItem item, DateOnly scanDay)
    {
        bool expired = item.ExpiryStatus == ExpiryStatusView.Expired || item.DaysRemaining < 0;

        NotificationSeverityLevel severity = expired
            ? NotificationSeverityLevel.Critical
            : NotificationSeverityLevel.Warning;

        string title = expired
            ? $"Controlado vencido: {item.Name}"
            : $"Controlado a vencer: {item.Name}";

        string description = BuildDescription(item, expired);

        return new RaiseNotificationRequest(
            Type: NotificationTypeCode.ControlledCompliance,
            Severity: severity,
            Title: title,
            Description: description,
            TargetType: TargetType,
            TargetId: item.Id,
            DedupeKey: BuildDedupeKey(item.Id, scanDay));
    }

    /// <summary>Composes the per-cycle idempotency key: <c>controlled:stock_item:{id}:{yyyy-MM-dd}</c>.</summary>
    internal static string BuildDedupeKey(Guid itemId, DateOnly scanDay)
        => $"controlled:{TargetType}:{itemId}:{scanDay:yyyy-MM-dd}";

    private static string BuildDescription(ExpiringItem item, bool expired)
    {
        string location = string.IsNullOrWhiteSpace(item.StorageLocationName)
            ? "local não informado"
            : item.StorageLocationName;

        if (expired)
        {
            int daysAgo = Math.Abs(item.DaysRemaining);
            string when = daysAgo == 0 ? "hoje" : $"há {daysAgo} dia(s)";
            return $"Vencido {when} — {location}. Verifique o descarte formal.";
        }

        string dueIn = item.DaysRemaining == 0 ? "hoje" : $"em {item.DaysRemaining} dia(s)";
        return $"Vence {dueIn} — {location}. Antecipe a renovação ou o descarte.";
    }
}
