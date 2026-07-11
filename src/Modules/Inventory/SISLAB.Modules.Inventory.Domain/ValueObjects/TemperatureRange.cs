using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Domain.ValueObjects;

/// <summary>
/// Target conservation temperature range of a refrigerated storage location, in degrees Celsius (for
/// example -20 °C to -80 °C for a freezer). This is the range the location is expected to hold, so cold
/// stock is stored where its conservation requirement is met; it is not a live sensor reading. Device
/// monitoring belongs to Equipment (later module).
/// </summary>
public sealed class TemperatureRange : ValueObject
{
    private TemperatureRange(decimal minimumCelsius, decimal maximumCelsius)
    {
        MinimumCelsius = minimumCelsius;
        MaximumCelsius = maximumCelsius;
    }

    public decimal MinimumCelsius { get; }

    public decimal MaximumCelsius { get; }

    /// <summary>
    /// Builds a range from its inclusive bounds. The minimum must not exceed the maximum; equal bounds
    /// describe a single target temperature.
    /// </summary>
    public static TemperatureRange Between(decimal minimumCelsius, decimal maximumCelsius)
    {
        if (minimumCelsius > maximumCelsius)
            throw new DomainException(
                $"Temperature range minimum ({minimumCelsius} °C) cannot be greater than the maximum ({maximumCelsius} °C).");

        return new TemperatureRange(minimumCelsius, maximumCelsius);
    }

    public bool Includes(decimal celsius) => celsius >= MinimumCelsius && celsius <= MaximumCelsius;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MinimumCelsius;
        yield return MaximumCelsius;
    }

    public override string ToString() => $"{MinimumCelsius} °C to {MaximumCelsius} °C";
}
