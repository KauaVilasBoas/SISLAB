using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Domain.ValueObjects;

/// <summary>
/// A non-negative amount qualified by a <see cref="UnitOfMeasure"/>. Arithmetic is only allowed
/// between quantities sharing the same unit; the MVP does not convert between units. Immutable:
/// every operation returns a new instance.
/// </summary>
public sealed class Quantity : ValueObject
{
    private Quantity(decimal value, UnitOfMeasure unit)
    {
        Value = value;
        Unit = unit;
    }

    public decimal Value { get; }

    public UnitOfMeasure Unit { get; }

    public bool IsZero => Value == 0m;

    public static Quantity Of(decimal value, UnitOfMeasure unit)
    {
        if (unit is null)
            throw new DomainException("Quantity requires a unit of measure.");

        if (value < 0m)
            throw new DomainException($"Quantity cannot be negative. Received: {value} {unit}.");

        return new Quantity(value, unit);
    }

    public static Quantity Zero(UnitOfMeasure unit) => Of(0m, unit);

    public Quantity Add(Quantity other)
    {
        EnsureSameUnit(other);
        return new Quantity(Value + other.Value, Unit);
    }

    /// <summary>
    /// Subtracts another quantity of the same unit, rejecting results that would go below zero.
    /// Guards the "no negative stock" invariant at the value-object level.
    /// </summary>
    public Quantity Subtract(Quantity other)
    {
        EnsureSameUnit(other);

        decimal result = Value - other.Value;
        if (result < 0m)
            throw new DomainException(
                $"Subtracting {other} from {this} would result in a negative quantity.");

        return new Quantity(result, Unit);
    }

    public bool IsGreaterThanOrEqualTo(Quantity other)
    {
        EnsureSameUnit(other);
        return Value >= other.Value;
    }

    public bool IsLessThan(Quantity other)
    {
        EnsureSameUnit(other);
        return Value < other.Value;
    }

    private void EnsureSameUnit(Quantity other)
    {
        if (other is null)
            throw new DomainException("Cannot operate with a null quantity.");

        if (!Unit.IsCompatibleWith(other.Unit))
            throw new DomainException(
                $"Cannot combine quantities with incompatible units: '{Unit}' and '{other.Unit}'.");
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
        yield return Unit;
    }

    public override string ToString() => $"{Value} {Unit}";
}
