using SISLAB.Modules.Configuration.Domain.ReferenceRanges;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Configuration.Tests.Domain.ReferenceRanges;

/// <summary>
/// Covers the <see cref="RangeBounds"/> value object (card [E12] #76): the "min ≤ max, at least one bound"
/// invariant, open-ended bounds via null, inclusive containment and structural equality.
/// </summary>
public sealed class RangeBoundsTests
{
    [Fact]
    public void Of_accepts_a_closed_interval()
    {
        RangeBounds bounds = RangeBounds.Of(12m, 16m);

        Assert.Equal(12m, bounds.Minimum);
        Assert.Equal(16m, bounds.Maximum);
    }

    [Fact]
    public void Of_accepts_an_open_lower_end()
    {
        RangeBounds bounds = RangeBounds.Of(null, 5m);

        Assert.Null(bounds.Minimum);
        Assert.Equal(5m, bounds.Maximum);
    }

    [Fact]
    public void Of_accepts_an_open_upper_end()
    {
        RangeBounds bounds = RangeBounds.Of(12m, null);

        Assert.Equal(12m, bounds.Minimum);
        Assert.Null(bounds.Maximum);
    }

    [Fact]
    public void Of_rejects_an_interval_with_no_bound()
    {
        Assert.Throws<DomainException>(() => RangeBounds.Of(null, null));
    }

    [Fact]
    public void Of_rejects_an_inverted_interval()
    {
        Assert.Throws<DomainException>(() => RangeBounds.Of(16m, 12m));
    }

    [Fact]
    public void Of_accepts_a_single_point_interval()
    {
        RangeBounds bounds = RangeBounds.Of(10m, 10m);

        Assert.True(bounds.Contains(10m));
    }

    [Theory]
    [InlineData(11.9, false)]
    [InlineData(12.0, true)]
    [InlineData(14.0, true)]
    [InlineData(16.0, true)]
    [InlineData(16.1, false)]
    public void Contains_is_inclusive_of_both_bounds(double value, bool expected)
    {
        RangeBounds bounds = RangeBounds.Of(12m, 16m);

        Assert.Equal(expected, bounds.Contains((decimal)value));
    }

    [Fact]
    public void Contains_treats_a_null_bound_as_open()
    {
        RangeBounds atMost = RangeBounds.Of(null, 5m);

        Assert.True(atMost.Contains(-100m));
        Assert.True(atMost.Contains(5m));
        Assert.False(atMost.Contains(5.1m));
    }

    [Fact]
    public void Equality_is_structural()
    {
        Assert.Equal(RangeBounds.Of(1m, 2m), RangeBounds.Of(1m, 2m));
        Assert.NotEqual(RangeBounds.Of(1m, 2m), RangeBounds.Of(1m, 3m));
    }
}
