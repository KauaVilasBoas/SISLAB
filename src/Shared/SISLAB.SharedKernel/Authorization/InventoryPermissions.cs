namespace SISLAB.SharedKernel.Authorization;

/// <summary>
/// Permission-code catalogue for the Inventory bounded context.
///
/// <para><b>Convention (enforced by Lumen 1.1.0):</b> every code is <c>&lt;Controller&gt;.&lt;Action&gt;</c>
/// — the controller class name without the <c>Controller</c> suffix, plus the action method name, in
/// PascalCase exactly as in C#. Lumen's <c>Permission.Create</c> recomputes the code from
/// controller + action and <b>ignores</b> any string passed to <c>[RequirePermission]</c>; these
/// constants therefore exist only to remove magic strings from the Role→permissions map, tests and
/// consumers. Each constant must map 1:1 to a real controller.action pair — an ArchTest guards the
/// binding so a renamed action breaks the build until the constant is updated.</para>
///
/// <para>Only <b>write</b> actions (POST/PUT/DELETE/PATCH) need permission constants: reads (GET) are
/// authenticated but not permission-gated (any member, including <c>ReadOnly</c>, may read).</para>
/// </summary>
public static class InventoryPermissions
{
    /// <summary>Write permissions on <c>StockController</c> (prefix <c>Stock</c>).</summary>
    public static class Stock
    {
        public const string RegisterStockItem = "Stock.RegisterStockItem";
        public const string RegisterEntry = "Stock.RegisterEntry";
        public const string RegisterConsumption = "Stock.RegisterConsumption";
        public const string Transfer = "Stock.Transfer";
        public const string Dispose = "Stock.Dispose";
        public const string RegisterCount = "Stock.RegisterCount";
    }

    /// <summary>Write permissions on <c>EquipmentController</c> (prefix <c>Equipment</c>).</summary>
    public static class Equipment
    {
        public const string Register = "Equipment.Register";
        public const string Update = "Equipment.Update";
        public const string ChangeStatus = "Equipment.ChangeStatus";
        public const string DefineCalibration = "Equipment.DefineCalibration";
        public const string RecordMaintenance = "Equipment.RecordMaintenance";
    }

    /// <summary>Write permissions on <c>PartnersController</c> (prefix <c>Partners</c>).</summary>
    public static class Partners
    {
        public const string Register = "Partners.Register";
        public const string Update = "Partners.Update";
        public const string Deactivate = "Partners.Deactivate";
        public const string Reactivate = "Partners.Reactivate";
        public const string RecordSample = "Partners.RecordSample";
        public const string RemoveSample = "Partners.RemoveSample";
    }

    /// <summary>Every Inventory write permission — the full set granted to a Coordinator.</summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>
    {
        Stock.RegisterStockItem, Stock.RegisterEntry, Stock.RegisterConsumption,
        Stock.Transfer, Stock.Dispose, Stock.RegisterCount,
        Equipment.Register, Equipment.Update, Equipment.ChangeStatus,
        Equipment.DefineCalibration, Equipment.RecordMaintenance,
        Partners.Register, Partners.Update, Partners.Deactivate,
        Partners.Reactivate, Partners.RecordSample, Partners.RemoveSample
    };
}
