using SISLAB.Jobs.Jobs;
using SISLAB.Modules.Inventory.Application.Equipments.Queries;
using SISLAB.Modules.Inventory.Application.StockRead;
using SISLAB.Modules.Notifications.Contracts;

namespace SISLAB.Jobs.Tests.Jobs;

/// <summary>
/// Pure-policy tests for the three alert jobs' mapping rules (#41/#42/#66): they pin the severity banding,
/// the notification type/target and the per-cycle dedupe key without a host, a database or a clock.
/// </summary>
public sealed class AlertPolicyTests
{
    private static readonly DateOnly ScanDay = new(2026, 7, 13);

    // ------------------------------------ Expiry (#41) ------------------------------------

    [Theory]
    [InlineData(ExpiryStatusView.Expired, -3, NotificationSeverityLevel.Critical)]
    [InlineData(ExpiryStatusView.ExpiringSoon, 0, NotificationSeverityLevel.Warning)]
    [InlineData(ExpiryStatusView.ExpiringSoon, 7, NotificationSeverityLevel.Warning)]
    [InlineData(ExpiryStatusView.ExpiringSoon, 8, NotificationSeverityLevel.Info)]
    [InlineData(ExpiryStatusView.ExpiringSoon, 15, NotificationSeverityLevel.Info)]
    [InlineData(ExpiryStatusView.ExpiringSoon, 30, NotificationSeverityLevel.Info)]
    public void Expiry_severity_bands_by_days_remaining(
        ExpiryStatusView status, int daysRemaining, NotificationSeverityLevel expected)
    {
        ExpiringItem item = ExpiringItem(status, daysRemaining);

        Assert.Equal(expected, ExpiryAlertPolicy.ClassifySeverity(item));
    }

    [Fact]
    public void Expiry_notification_carries_type_target_and_day_bucketed_dedupe_key()
    {
        ExpiringItem item = ExpiringItem(ExpiryStatusView.ExpiringSoon, 5);

        RaiseNotificationRequest request = ExpiryAlertPolicy.ToNotification(item, ScanDay);

        Assert.Equal(NotificationTypeCode.Expiry, request.Type);
        Assert.Equal("stock_item", request.TargetType);
        Assert.Equal(item.Id, request.TargetId);
        Assert.Equal($"expiry:stock_item:{item.Id}:2026-07-13", request.DedupeKey);
    }

    [Fact]
    public void Expiry_dedupe_key_changes_per_day_so_the_alert_can_re_fire_next_cycle()
    {
        ExpiringItem item = ExpiringItem(ExpiryStatusView.ExpiringSoon, 5);

        string today = ExpiryAlertPolicy.BuildDedupeKey(item.Id, ScanDay);
        string tomorrow = ExpiryAlertPolicy.BuildDedupeKey(item.Id, ScanDay.AddDays(1));

        Assert.NotEqual(today, tomorrow);
    }

    // ------------------------------------ Low stock (#42) ------------------------------------

    [Fact]
    public void LowStock_controlled_item_is_critical_otherwise_warning()
    {
        Assert.Equal(NotificationSeverityLevel.Critical,
            LowStockAlertPolicy.ClassifySeverity(BelowMinimumItem(isControlled: true)));
        Assert.Equal(NotificationSeverityLevel.Warning,
            LowStockAlertPolicy.ClassifySeverity(BelowMinimumItem(isControlled: false)));
    }

    [Fact]
    public void LowStock_notification_carries_type_target_and_day_bucketed_dedupe_key()
    {
        BelowMinimumItem item = BelowMinimumItem(isControlled: false);

        RaiseNotificationRequest request = LowStockAlertPolicy.ToNotification(item, ScanDay);

        Assert.Equal(NotificationTypeCode.LowStock, request.Type);
        Assert.Equal("stock_item", request.TargetType);
        Assert.Equal(item.Id, request.TargetId);
        Assert.Equal($"lowstock:stock_item:{item.Id}:2026-07-13", request.DedupeKey);
    }

    // ------------------------------------ Calibration (#66) ------------------------------------

    [Fact]
    public void Calibration_notification_is_warning_with_type_target_and_day_bucketed_dedupe_key()
    {
        OverdueCalibrationEquipment equipment = OverdueEquipment(daysOverdue: 12);

        RaiseNotificationRequest request = CalibrationAlertPolicy.ToNotification(equipment, ScanDay);

        Assert.Equal(NotificationTypeCode.Calibration, request.Type);
        Assert.Equal(NotificationSeverityLevel.Warning, request.Severity);
        Assert.Equal("equipment", request.TargetType);
        Assert.Equal(equipment.Id, request.TargetId);
        Assert.Equal($"calibration:equipment:{equipment.Id}:2026-07-13", request.DedupeKey);
    }

    // ------------------------------------ builders ------------------------------------

    private static ExpiringItem ExpiringItem(ExpiryStatusView status, int daysRemaining) => new(
        Id: Guid.NewGuid(),
        Name: "Kit Griess (NO)",
        Category: "Reagente",
        LotCode: "L-42",
        Quantity: 3m,
        Unit: "un",
        ExpiryYear: 2026,
        ExpiryMonth: 7,
        ExpiryStatus: status,
        DaysRemaining: daysRemaining,
        IsControlled: false,
        StorageLocationId: Guid.NewGuid(),
        StorageLocationName: "Armário de Reagentes",
        StorageLocationType: "Cabinet");

    private static BelowMinimumItem BelowMinimumItem(bool isControlled) => new(
        Id: Guid.NewGuid(),
        Name: "Etanol PA",
        Category: "Solvente",
        Brand: "Merck",
        Quantity: 2m,
        Unit: "L",
        MinimumQuantity: 5m,
        MinimumUnit: "L",
        Deficit: 3m,
        IsControlled: isControlled,
        StorageLocationId: Guid.NewGuid(),
        StorageLocationName: "Depósito",
        StorageLocationType: "Room");

    private static OverdueCalibrationEquipment OverdueEquipment(int daysOverdue) => new(
        Id: Guid.NewGuid(),
        Name: "Balança Analítica",
        AssetTag: "EQ-001",
        Brand: "Shimadzu",
        Model: "AUW220D",
        LastCalibration: new DateOnly(2025, 7, 1),
        NextCalibration: ScanDay.AddDays(-daysOverdue),
        CalibrationStatus: CalibrationStatusView.Overdue,
        DaysOverdue: daysOverdue,
        StorageLocationId: Guid.NewGuid());
}
