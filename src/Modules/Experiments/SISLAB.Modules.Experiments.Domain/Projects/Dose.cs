using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// The dose a treatment <see cref="Group"/> receives in an in vivo study (card [E11] #73): a non-negative
/// numeric <see cref="Amount"/> in a free-text <see cref="Unit"/> (for example 10 mg/kg, 0 for the vehicle /
/// negative control). A value object — immutable and compared by value — so two groups with the same dose are
/// interchangeable and a group's dose is never mutated in place, only replaced.
/// </summary>
/// <remarks>
/// The unit is kept as free text rather than an enum on purpose: dosing conventions vary widely across studies
/// (mg/kg, µg, mL/kg, IU, …) and the laboratory records whichever it uses. A zero amount is legal — it models the
/// vehicle/control arm, which is a first-class group of any delineation.
/// </remarks>
public sealed class Dose : ValueObject
{
    private const int MaxUnitLength = 30;

    // Parameterless constructor for EF Core materialization of the owned value object.
    private Dose() => Unit = default!;

    private Dose(decimal amount, string unit)
    {
        Amount = amount;
        Unit = unit;
    }

    /// <summary>The dose magnitude; non-negative (zero models a vehicle / negative control).</summary>
    public decimal Amount { get; }

    /// <summary>The dose unit as recorded by the laboratory (e.g. "mg/kg", "µg", "mL/kg").</summary>
    public string Unit { get; }

    /// <summary>Builds a dose, guarding a non-negative amount and a present unit.</summary>
    public static Dose Of(decimal amount, string unit)
    {
        Guard.AgainstNegative(amount, nameof(amount));
        Guard.AgainstNullOrWhiteSpace(unit, nameof(unit));

        string trimmedUnit = unit.Trim();
        Guard.AgainstMaxLength(trimmedUnit, MaxUnitLength, nameof(unit));

        return new Dose(amount, trimmedUnit);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Unit;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Amount} {Unit}";
}
