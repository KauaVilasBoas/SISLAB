namespace SISLAB.Modules.Inventory.Application.Audit;

/// <summary>
/// Canonical action names for Inventory audit entries (card [E9] #57). Kept as constants so the trail uses
/// a stable vocabulary the compliance screen and its <c>Action</c> filter can rely on.
/// </summary>
internal static class InventoryAuditActions
{
    /// <summary>Consumption of a controlled item.</summary>
    public const string Consumption = "consumption";

    /// <summary>Disposal/discard of a controlled item.</summary>
    public const string Disposal = "disposal";

    /// <summary>Physical stock count (conference) of a controlled item.</summary>
    public const string StockCount = "stock-count";

    /// <summary>Equipment maintenance event.</summary>
    public const string EquipmentMaintenance = "equipment-maintenance";

    /// <summary>Equipment calibration schedule definition/change.</summary>
    public const string EquipmentCalibration = "equipment-calibration";
}
