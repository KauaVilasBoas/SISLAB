using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Domain.Equipments;

/// <summary>
/// Calibration schedule of an <see cref="Equipment"/>: when it was last calibrated and, optionally, when
/// it is next due. It classifies itself as <b>overdue</b> ("calibração atrasada") against a supplied
/// <see cref="IClock"/> when the due date has already passed, which feeds the summary pill and the
/// dashboard alert (card [E3] #27; the periodic scan/notification is the E6 job's job).
/// </summary>
/// <remarks>
/// <para>
/// A <see langword="null"/> <see cref="Equipment.Calibration"/> means calibration is <b>not applicable</b>
/// to that equipment (n/a — e.g. a Vortex or a Rota-rod), which the prototype renders as "—". So the
/// absence of a schedule is modelled by the absence of the value object, not by a special state inside it.
/// </para>
/// <para>
/// The next-due date is optional: some equipment is calibrated on record without a planned next date. An
/// equipment with no due date is never overdue. When present, the due date must not precede the last
/// calibration, so the schedule cannot describe an impossible timeline.
/// </para>
/// </remarks>
public sealed class CalibrationSchedule : ValueObject
{
    private CalibrationSchedule(DateOnly lastCalibration, DateOnly? nextCalibration)
    {
        LastCalibration = lastCalibration;
        NextCalibration = nextCalibration;
    }

    /// <summary>Date the equipment was last calibrated.</summary>
    public DateOnly LastCalibration { get; }

    /// <summary>Date the next calibration is due; <see langword="null"/> when none is planned.</summary>
    public DateOnly? NextCalibration { get; }

    /// <summary>
    /// Builds a schedule from the last calibration date and an optional next-due date. The next-due date,
    /// when supplied, must not fall before the last calibration.
    /// </summary>
    public static CalibrationSchedule Create(DateOnly lastCalibration, DateOnly? nextCalibration = null)
    {
        if (nextCalibration is { } due && due < lastCalibration)
            throw new DomainException(
                $"Next calibration ({due:yyyy-MM-dd}) cannot fall before the last calibration ({lastCalibration:yyyy-MM-dd}).");

        return new CalibrationSchedule(lastCalibration, nextCalibration);
    }

    /// <summary>
    /// True when a next calibration is due and its date is strictly before today (the "calibração
    /// atrasada" state). An equipment with no planned next date is never overdue. Derived on demand from
    /// the supplied clock; never persisted.
    /// </summary>
    public bool IsOverdue(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        return NextCalibration is { } due && due < DateOnly.FromDateTime(clock.UtcNow);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return LastCalibration;
        yield return NextCalibration;
    }

    public override string ToString() =>
        NextCalibration is { } due
            ? $"last {LastCalibration:MM/yyyy}, next {due:MM/yyyy}"
            : $"last {LastCalibration:MM/yyyy}";
}
