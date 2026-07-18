using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.SharedKernel.Domain;

/// <summary>
/// A target conservation temperature range in degrees Celsius (for example -20 °C to -80 °C for a freezer, or
/// 2 °C to 8 °C for a fridge). It is the range something is <b>expected to be kept at</b> — the conservation
/// requirement — not a live sensor reading. Device monitoring belongs to Equipment.
/// </summary>
/// <remarks>
/// Promoted to the SharedKernel (card [E11] #89) because more than one bounded context conserves things at a
/// controlled temperature: cold stock in the Inventory (a <c>StorageLocation</c>) and biological samples in the
/// Experiments biobank (a <c>Sample</c>). It is a pure value object — structural equality on its inclusive bounds,
/// no infrastructure — so it is safe to live in the shared, infra-free kernel.
/// </remarks>
public sealed class TemperatureRange : ValueObject
{
    private TemperatureRange(decimal minimumCelsius, decimal maximumCelsius)
    {
        MinimumCelsius = minimumCelsius;
        MaximumCelsius = maximumCelsius;
    }

    /// <summary>The inclusive lower bound of the conservation range, in degrees Celsius.</summary>
    public decimal MinimumCelsius { get; }

    /// <summary>The inclusive upper bound of the conservation range, in degrees Celsius.</summary>
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

    /// <summary>True when <paramref name="celsius"/> falls within the inclusive bounds of this range.</summary>
    public bool Includes(decimal celsius) => celsius >= MinimumCelsius && celsius <= MaximumCelsius;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MinimumCelsius;
        yield return MaximumCelsius;
    }

    public override string ToString() => $"{MinimumCelsius} °C to {MaximumCelsius} °C";
}
