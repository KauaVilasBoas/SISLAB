using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.SharedKernel.Tests.Domain;

/// <summary>
/// Structural-equality and invariant tests for the shared <see cref="TemperatureRange"/> value object
/// (promoted to the SharedKernel on card [E11] #89 so both the Inventory cold storage and the Experiments
/// biobank conserve at a controlled temperature through one type).
/// </summary>
public sealed class TemperatureRangeTests
{
    [Fact]
    public void Between_keeps_the_inclusive_bounds()
    {
        TemperatureRange range = TemperatureRange.Between(-80m, -20m);

        Assert.Equal(-80m, range.MinimumCelsius);
        Assert.Equal(-20m, range.MaximumCelsius);
    }

    [Fact]
    public void Between_allows_equal_bounds_as_a_single_target()
    {
        TemperatureRange range = TemperatureRange.Between(4m, 4m);

        Assert.Equal(4m, range.MinimumCelsius);
        Assert.Equal(4m, range.MaximumCelsius);
    }

    [Fact]
    public void Between_rejects_a_minimum_greater_than_the_maximum()
    {
        Assert.Throws<DomainException>(() => TemperatureRange.Between(8m, 2m));
    }

    [Theory]
    [InlineData(-80, true)]
    [InlineData(-50, true)]
    [InlineData(-20, true)]
    [InlineData(-19, false)]
    [InlineData(0, false)]
    public void Includes_reflects_the_closed_interval(decimal celsius, bool expected)
    {
        TemperatureRange range = TemperatureRange.Between(-80m, -20m);

        Assert.Equal(expected, range.Includes(celsius));
    }

    [Fact]
    public void Two_ranges_with_the_same_bounds_are_equal()
    {
        Assert.Equal(TemperatureRange.Between(2m, 8m), TemperatureRange.Between(2m, 8m));
        Assert.NotEqual(TemperatureRange.Between(2m, 8m), TemperatureRange.Between(2m, 6m));
    }
}
