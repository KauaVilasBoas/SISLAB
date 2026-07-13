namespace SISLAB.Modules.Inventory.Application.Equipments.Queries;

/// <summary>
/// Derived calibration classification of an equipment, as exposed to the read side (card [E4] #27). It mirrors
/// the prototype's "Equipamentos" screen (#48): equipment with no applicable calibration reads as n/a, and one
/// whose next calibration is near or past is highlighted ("calibração atrasada").
/// </summary>
public enum CalibrationStatus
{
    /// <summary>Calibration does not apply — there is no planned next calibration (the prototype's "n/a").</summary>
    NotRequired,

    /// <summary>The next calibration is planned and comfortably in the future.</summary>
    UpToDate,

    /// <summary>The next calibration falls within the warning window (about to be due).</summary>
    DueSoon,

    /// <summary>The next calibration date has already passed ("calibração atrasada").</summary>
    Overdue
}

/// <summary>
/// Read-side classification of an equipment's calibration schedule into a <see cref="CalibrationStatus"/>,
/// against a reference day and warning window. It is the single specification of the rule the equipment-listing
/// query derives (card [E4] #27): the SQL <c>CASE</c> in <see cref="ListEquipmentQuery"/> is a faithful mirror of
/// this method, and it agrees with the domain's "overdue when next_calibration is in the past" semantics
/// (<c>CalibrationSchedule.IsOverdue</c>).
/// </summary>
/// <remarks>
/// Kept as a pure function (no clock, no I/O) so the rule is unit-testable without a live database and so both the
/// C# read model and the SQL projection agree on the exact boundary conditions: <see cref="CalibrationStatus.NotRequired"/>
/// with no planned next date; <see cref="CalibrationStatus.Overdue"/> strictly before <c>today</c>;
/// <see cref="CalibrationStatus.DueSoon"/> when the next date falls within the window from <c>today</c>; otherwise
/// <see cref="CalibrationStatus.UpToDate"/>.
/// </remarks>
internal static class CalibrationStatusRule
{
    /// <summary>Default window that classifies a calibration as "due soon" — matches the card's 30-day highlight.</summary>
    internal const int DefaultDueSoonWindowDays = 30;

    /// <summary>
    /// Classifies the calibration given by <paramref name="nextCalibration"/> against <paramref name="today"/>.
    /// Returns <see cref="CalibrationStatus.NotRequired"/> when there is no planned next date (calibration n/a).
    /// </summary>
    internal static CalibrationStatus Classify(
        DateOnly? nextCalibration,
        DateOnly today,
        int dueSoonWindowDays = DefaultDueSoonWindowDays)
    {
        if (nextCalibration is not { } next)
            return CalibrationStatus.NotRequired;

        if (next < today)
            return CalibrationStatus.Overdue;

        DateOnly dueSoonThreshold = today.AddDays(dueSoonWindowDays);
        return next <= dueSoonThreshold
            ? CalibrationStatus.DueSoon
            : CalibrationStatus.UpToDate;
    }
}
