namespace SISLAB.Modules.Inventory.Domain.Equipments;

/// <summary>
/// Operational status of a laboratory <see cref="Equipment"/>, taken from the real prototype (screen
/// "Equipamentos" #48). The aggregate only allows a defined set of transitions between these values
/// (see <see cref="Equipment.ChangeStatus"/>).
/// </summary>
public enum EquipmentStatus
{
    /// <summary>Currently running an assay/being operated ("Em uso").</summary>
    InUse,

    /// <summary>Idle and ready to be used ("Disponível").</summary>
    Available,

    /// <summary>Out of service for repair/servicing ("Manutenção"). Cannot be put in use directly.</summary>
    UnderMaintenance,

    /// <summary>Retired/decommissioned ("Inativo"). A terminal status until explicitly reactivated.</summary>
    Inactive
}
