namespace SISLAB.Modules.Experiments.Application.Protocols;

/// <summary>
/// Sample mean and standard deviation over a set of replicate values (SISLAB-07 — grouping replicates of the
/// same condition). Uses the <b>sample</b> standard deviation (n − 1 denominator, Bessel's correction), the same
/// convention GraphPad Prism and the researcher's spreadsheet use; a single replicate has an undefined SD, so it
/// is reported as null rather than zero.
/// </summary>
/// <remarks>
/// The number of replicates is whatever the plate carries — triplicate is the common case, but the aggregation is
/// count-agnostic (SISLAB-07: "nº de réplicas é parâmetro, não fixo em 3"). Excluded outliers never reach here:
/// the strategies feed only the wells that count toward the calculation.
/// </remarks>
internal static class ReplicateStatistics
{
    /// <summary>
    /// Computes the replicate count, mean and sample standard deviation of <paramref name="values"/>. An empty set
    /// yields <c>(0, 0, null)</c> and a single value yields <c>(1, value, null)</c> — SD is undefined below two.
    /// </summary>
    public static (int Count, decimal Mean, decimal? StandardDeviation) Compute(IReadOnlyList<decimal> values)
    {
        int count = values.Count;
        if (count == 0)
            return (0, 0m, null);

        decimal mean = values.Sum() / count;
        if (count < 2)
            return (count, mean, null);

        decimal sumOfSquares = values.Sum(value => (value - mean) * (value - mean));
        double variance = (double)sumOfSquares / (count - 1);
        var standardDeviation = (decimal)Math.Sqrt(variance);

        return (count, mean, standardDeviation);
    }
}
