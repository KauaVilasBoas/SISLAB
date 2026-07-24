using SISLAB.Modules.Configuration.Domain.ExperimentalModels;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Configuration.Tests.Domain.ExperimentalModels;

/// <summary>
/// Covers the <see cref="InductionProtocol"/> value object (SISLAB-04): administration/interval/reference-day
/// invariants that keep a model from holding a nonsensical protocol.
/// </summary>
public sealed class InductionProtocolTests
{
    [Fact]
    public void Of_builds_a_multi_administration_protocol()
    {
        InductionProtocol protocol = InductionProtocol.Of(administrations: 2, intervalDays: 1, referenceDayAfterInduction: 28);

        Assert.Equal(2, protocol.Administrations);
        Assert.Equal(1, protocol.IntervalDays);
        Assert.Equal(28, protocol.ReferenceDayAfterInduction);
    }

    [Fact]
    public void Of_allows_a_single_administration_with_no_interval()
    {
        InductionProtocol protocol = InductionProtocol.Of(1, 0, 14);

        Assert.Equal(1, protocol.Administrations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Of_rejects_fewer_than_one_administration(int administrations)
    {
        Assert.Throws<DomainException>(() => InductionProtocol.Of(administrations, 0, 0));
    }

    [Fact]
    public void Of_rejects_a_single_administration_with_an_interval()
    {
        Assert.Throws<DomainException>(() => InductionProtocol.Of(1, 3, 28));
    }

    [Fact]
    public void Of_rejects_a_negative_interval()
    {
        Assert.Throws<DomainException>(() => InductionProtocol.Of(2, -1, 28));
    }

    [Fact]
    public void Of_rejects_a_negative_reference_day()
    {
        Assert.Throws<DomainException>(() => InductionProtocol.Of(2, 1, -1));
    }

    [Fact]
    public void Protocols_with_the_same_values_are_equal()
    {
        Assert.Equal(InductionProtocol.Of(2, 1, 28), InductionProtocol.Of(2, 1, 28));
    }
}
