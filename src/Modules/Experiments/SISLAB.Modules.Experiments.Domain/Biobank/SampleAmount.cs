using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Biobank;

/// <summary>
/// A quantity of biological material — a non-negative numeric <see cref="Value"/> in a free-text
/// <see cref="Unit"/> (for example 1.5 mL, 200 µL, 50 mg — card [E11] #89). A value object: immutable, compared
/// by value, so a sample's collected amount and each analysis' consumed amount are the same expressible type and
/// the biobank's derived balance is a plain subtraction over a shared unit.
/// </summary>
/// <remarks>
/// The unit is free text, mirroring the module's <c>Dose</c> value object, because biobank aliquots are measured
/// in whatever unit the laboratory records (mL/µL for fluids, mg for tissue). Arithmetic (<see cref="Subtract"/>)
/// is only defined between amounts of the <b>same</b> unit — mixing units is a domain error, never a silent
/// conversion — which keeps the derived balance trustworthy.
/// </remarks>
public sealed class SampleAmount : ValueObject
{
    private const int MaxUnitLength = 30;

    // Parameterless constructor for EF Core materialization of the owned value object.
    private SampleAmount() => Unit = default!;

    private SampleAmount(decimal value, string unit)
    {
        Value = value;
        Unit = unit;
    }

    /// <summary>The magnitude of the amount; non-negative.</summary>
    public decimal Value { get; }

    /// <summary>The unit of measure as recorded by the laboratory (e.g. "mL", "µL", "mg").</summary>
    public string Unit { get; }

    /// <summary>Builds an amount, guarding a non-negative value and a present unit.</summary>
    public static SampleAmount Of(decimal value, string unit)
    {
        Guard.AgainstNegative(value, nameof(value));
        Guard.AgainstNullOrWhiteSpace(unit, nameof(unit));

        string trimmedUnit = unit.Trim();
        Guard.AgainstMaxLength(trimmedUnit, MaxUnitLength, nameof(unit));

        return new SampleAmount(value, trimmedUnit);
    }

    /// <summary>True when this amount and <paramref name="other"/> are expressed in the same unit.</summary>
    public bool HasSameUnitAs(SampleAmount other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(Unit, other.Unit, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Subtracts <paramref name="other"/> from this amount. Both must share the unit; subtracting more than the
    /// available value is a domain error (the balance may not go negative).
    /// </summary>
    public SampleAmount Subtract(SampleAmount other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!HasSameUnitAs(other))
            throw new DomainException(
                $"Cannot subtract amounts in different units ('{Unit}' vs '{other.Unit}').");

        if (other.Value > Value)
            throw new DomainException(
                $"Cannot subtract {other} from {this}: the result would be negative.");

        return new SampleAmount(Value - other.Value, Unit);
    }

    /// <summary>True when this amount can absorb a subtraction of <paramref name="other"/> without going negative.</summary>
    public bool CanCover(SampleAmount other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return HasSameUnitAs(other) && other.Value <= Value;
    }

    /// <summary>True when the magnitude is zero (the sample is depleted).</summary>
    public bool IsZero => Value == 0m;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
        yield return Unit;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Value} {Unit}";
}
