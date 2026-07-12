namespace SISLAB.Modules.Inventory.Domain.Equipments;

/// <summary>
/// Nature of a <see cref="MaintenanceRecord"/> logged against an <see cref="Equipment"/>. Distinguishing
/// the kind of servicing lets the laboratory tell a planned check from a reaction to a failure, which
/// feeds traceability and reliability reports.
/// </summary>
public enum MaintenanceType
{
    /// <summary>Scheduled, routine servicing done to prevent failures ("Preventiva").</summary>
    Preventive,

    /// <summary>Servicing done to fix a fault after it happened ("Corretiva").</summary>
    Corrective,

    /// <summary>Adjustment/verification of measurement accuracy ("Calibração").</summary>
    Calibration
}
