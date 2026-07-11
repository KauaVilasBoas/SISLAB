namespace SISLAB.Modules.Inventory.Domain.StockItems;

/// <summary>
/// Category of a laboratory stock item, taken from the real LAFTE catalogue. Modelled as a closed
/// enum rather than free text so the domain and read models can reason about categories in a
/// type-safe way; controlled substances additionally carry the <c>IsControlled</c> flag on the item
/// itself, independent of category (decision recorded on card [E3] #21).
/// </summary>
public enum StockItemCategory
{
    Reagent,
    Solvent,
    Kit,
    Drug,
    Disposable,
    Supply,
    ControlledAnesthetic,
    ControlledOpioid,
    TestCompound
}
