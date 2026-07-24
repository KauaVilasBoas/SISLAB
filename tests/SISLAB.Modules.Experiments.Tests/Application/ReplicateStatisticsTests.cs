using SISLAB.Modules.Experiments.Application.Protocols;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// Unit tests for the replicate statistics helper (SISLAB-07): sample standard deviation (n − 1), a null SD below
/// two values, and count-agnostic behaviour (not fixed at three replicates).
/// </summary>
public sealed class ReplicateStatisticsTests
{
    [Fact]
    public void Empty_set_reports_zero_count_and_null_deviation()
    {
        (int count, decimal mean, decimal? sd) = ReplicateStatistics.Compute([]);

        Assert.Equal(0, count);
        Assert.Equal(0m, mean);
        Assert.Null(sd);
    }

    [Fact]
    public void A_single_value_has_a_mean_but_an_undefined_deviation()
    {
        (int count, decimal mean, decimal? sd) = ReplicateStatistics.Compute([42m]);

        Assert.Equal(1, count);
        Assert.Equal(42m, mean);
        Assert.Null(sd);
    }

    [Fact]
    public void Uses_the_sample_standard_deviation_with_n_minus_one()
    {
        // 84, 85, 86 → mean 85, sample variance = (1+0+1)/2 = 1, sample SD = 1.
        (int count, decimal mean, decimal? sd) = ReplicateStatistics.Compute([84m, 85m, 86m]);

        Assert.Equal(3, count);
        Assert.Equal(85m, mean);
        Assert.Equal(1m, Math.Round(sd!.Value, 4));
    }

    [Fact]
    public void Aggregation_is_not_fixed_at_three_replicates()
    {
        (int count, decimal mean, _) = ReplicateStatistics.Compute([10m, 20m, 30m, 40m, 50m]);

        Assert.Equal(5, count);
        Assert.Equal(30m, mean);
    }
}
