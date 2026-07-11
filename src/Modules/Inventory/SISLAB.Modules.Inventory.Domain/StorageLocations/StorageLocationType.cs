namespace SISLAB.Modules.Inventory.Domain.StorageLocations;

/// <summary>
/// Kind of storage location, taken from the real LAFTE layout. The type drives the storage rules the
/// aggregate enforces: <see cref="Controlled"/> is the only type allowed to hold controlled items, and
/// <see cref="Refrigerated"/> is the only type that may declare a target temperature range.
/// </summary>
public enum StorageLocationType
{
    /// <summary>General warehouse / stockroom ("almoxarifado").</summary>
    GeneralStorage,

    /// <summary>Reagent cabinet ("armário de reagentes").</summary>
    ReagentCabinet,

    /// <summary>Refrigerated storage: freezer or fridge, optionally with a target temperature range.</summary>
    Refrigerated,

    /// <summary>Restricted-access box/cabinet for controlled substances ("caixa/armário de controlados").</summary>
    Controlled,

    /// <summary>Partner-supplied substances ("Substâncias PARCEIROS"): externally-owned samples/compounds.</summary>
    Partner
}
