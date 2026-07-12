using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Application.StockRead;

/// <summary>
/// Granularity of a <see cref="GetConsumptionSeriesQuery"/> time bucket. Approved windows (card [E4] #31):
/// <see cref="Day"/> for the 7-day and 30-day views, <see cref="Month"/> for the 3-month view. When the
/// caller does not pin a bucket, the handler derives it from the window width (see
/// <see cref="GetConsumptionSeriesQuery"/>).
/// </summary>
public enum ConsumptionBucket
{
    /// <summary>One point per calendar day (<c>date_trunc('day', ...)</c>).</summary>
    Day,

    /// <summary>One point per calendar month (<c>date_trunc('month', ...)</c>).</summary>
    Month
}

/// <summary>
/// Read-side query (card [E4] #31) that builds the <b>consumption time series</b> of the <b>active company</b>
/// for the dashboard chart: total consumption bucketed by day or month over <c>[@From, @To]</c>, plus the
/// period total and the % delta against the immediately-preceding period of the same length. It reads the
/// projected ledger <c>inventory.stock_movements</c> (card [E4] #33) via Dapper — never the write DbContext —
/// keeping only the consumption movements (<c>movement_type = 'Consumed'</c>). Not paginated: a series is short.
/// </summary>
/// <remarks>
/// <para>
/// <b>Granularity.</b> Buckets come from <c>date_trunc(@Bucket, occurred_on)</c> — <see cref="ConsumptionBucket.Day"/>
/// or <see cref="ConsumptionBucket.Month"/>. The caller may pin the bucket; when it does not, the handler
/// derives it from the window width (<see cref="DeriveBucket"/>): windows up to ~2 months are daily, wider
/// windows are monthly — matching the approved 7d/30d = day and 3-month = month behaviour.
/// </para>
/// <para>
/// <b>Total, not by item — but by unit.</b> The series is the company-wide consumption, <b>not</b> broken
/// down by item. It is, however, kept <b>per unit</b>: the read side never converts between units, so summing
/// <c>mL</c> and <c>L</c> into one number would be meaningless. Each <see cref="ConsumptionSeriesPoint"/>
/// therefore carries its <see cref="ConsumptionSeriesPoint.Unit"/>, and the period total / delta are computed
/// per unit (<see cref="ConsumptionSeries.Totals"/>). A single-unit lab reads exactly one total, as expected.
/// </para>
/// <para>
/// <b>Delta.</b> The comparison window is the same-length period immediately before <c>[@From, @To]</c>: for a
/// 30-day window it is the previous 30 days. The handler computes those bounds; the SQL sums both windows in
/// one round-trip, and <see cref="ConsumptionDelta.Compute"/> turns (current, previous) into a signed
/// percentage. When the previous period had no consumption in a unit the percentage is undefined (division by
/// zero) and left null — the UI shows "novo"/"—" rather than a fake +100%.
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from
/// the request, and every SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global
/// query filter, so the tenant guard is explicit (defense-in-depth, section 7).
/// </para>
/// </remarks>
public sealed record GetConsumptionSeriesQuery : IQuery<ConsumptionSeries>
{
    /// <summary>Inclusive first day of the series window (matched against <c>occurred_on</c>).</summary>
    public DateOnly From { get; init; }

    /// <summary>Inclusive last day of the series window (matched against <c>occurred_on</c>).</summary>
    public DateOnly To { get; init; }

    /// <summary>Optional granularity; null lets the handler derive it from the window width.</summary>
    public ConsumptionBucket? Bucket { get; init; }

    /// <summary>Optional experiment filter; null gives company-wide consumption (with and without experiment).</summary>
    public Guid? ExperimentId { get; init; }
}

/// <summary>
/// The consumption time series for the active company (card [E4] #31): the ordered bucket points, the
/// per-unit period totals with their delta, and the effective granularity the handler used.
/// </summary>
public sealed record ConsumptionSeries(
    ConsumptionBucket Bucket,
    IReadOnlyList<ConsumptionSeriesPoint> Points,
    IReadOnlyList<ConsumptionPeriodTotal> Totals);

/// <summary>One point of the series: the total consumed of a single unit within a single bucket.</summary>
public sealed record ConsumptionSeriesPoint(DateOnly BucketStart, string Unit, decimal TotalConsumed);

/// <summary>
/// The per-unit period total and its delta versus the same-length preceding period. Kept per-unit because
/// the read side never converts between units.
/// </summary>
public sealed record ConsumptionPeriodTotal(
    string Unit,
    decimal CurrentTotal,
    decimal PreviousTotal,
    decimal? DeltaPercentage);

internal sealed class GetConsumptionSeriesQueryHandler
    : BaseDataAccess, IQueryHandler<GetConsumptionSeriesQuery, ConsumptionSeries>
{
    /// <summary>The persisted <c>movement_type</c> discriminator for a consumption (see the projection).</summary>
    private const string ConsumedMovementType = "Consumed";

    // Two result sets in one round-trip (Dapper QueryMultiple):
    //   1) the ordered (bucket, unit) points over the CURRENT window [@From, @To], truncated to the
    //      chosen granularity via date_trunc(@Bucket, ...); occurred_on is a date, cast to timestamp for
    //      date_trunc and back to date so a bucket start is a clean date.
    //   2) the per-unit totals of BOTH the current window and the same-length previous window
    //      [@PreviousFrom, @PreviousTo], via COUNT/SUM FILTER, so the delta needs no second query.
    // Both keep only consumption movements of the tenant company (WHERE company_id) and honour the optional
    // experiment filter. Grain is per unit — the read side never converts between units.
    private const string Sql =
        """
        SELECT
            date_trunc(@Bucket, m.occurred_on::timestamp)::date AS bucketstart,
            m.quantity_unit                                     AS unit,
            SUM(m.quantity_amount)                              AS totalconsumed
        FROM inventory.stock_movements AS m
        WHERE m.company_id = @CompanyId
          AND m.movement_type = @ConsumedMovementType
          AND m.occurred_on IS NOT NULL
          AND m.occurred_on BETWEEN @From AND @To
          AND (@ExperimentId IS NULL OR m.experiment_id = @ExperimentId)
        GROUP BY 1, m.quantity_unit
        ORDER BY 1 ASC, m.quantity_unit ASC;

        SELECT
            m.quantity_unit AS unit,
            COALESCE(SUM(m.quantity_amount) FILTER (
                WHERE m.occurred_on BETWEEN @From AND @To), 0)                 AS currenttotal,
            COALESCE(SUM(m.quantity_amount) FILTER (
                WHERE m.occurred_on BETWEEN @PreviousFrom AND @PreviousTo), 0) AS previoustotal
        FROM inventory.stock_movements AS m
        WHERE m.company_id = @CompanyId
          AND m.movement_type = @ConsumedMovementType
          AND m.occurred_on IS NOT NULL
          AND m.occurred_on BETWEEN @PreviousFrom AND @To
          AND (@ExperimentId IS NULL OR m.experiment_id = @ExperimentId)
        GROUP BY m.quantity_unit
        ORDER BY m.quantity_unit ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public GetConsumptionSeriesQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<ConsumptionSeries> HandleAsync(
        GetConsumptionSeriesQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        ConsumptionBucket bucket = ResolveBucket(request);
        ConsumptionSeriesQueryParameters parameters = BuildParameters(request);

        using SqlMapper.GridReader reader = await connection.QueryMultipleAsync(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken));

        IReadOnlyList<ConsumptionSeriesPoint> points = (await reader.ReadAsync<ConsumptionSeriesPoint>()).AsList();
        IReadOnlyList<PeriodTotalRow> totalRows = (await reader.ReadAsync<PeriodTotalRow>()).AsList();

        IReadOnlyList<ConsumptionPeriodTotal> totals = totalRows
            .Select(row => row.ToPeriodTotal())
            .ToList();

        return new ConsumptionSeries(bucket, points, totals);
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes from
    /// <see cref="ITenantContext"/> (never the request); the resolved bucket becomes the <c>date_trunc</c> unit;
    /// and the same-length previous window is derived from <c>[From, To]</c>. Extracted so the tenant guard, the
    /// bucket resolution and the previous-window math are unit-testable without a live database.
    /// </summary>
    internal ConsumptionSeriesQueryParameters BuildParameters(GetConsumptionSeriesQuery request)
    {
        ConsumptionWindow.EnsureValid(request.From, request.To);

        (DateOnly previousFrom, DateOnly previousTo) = PreviousWindow(request.From, request.To);

        return new ConsumptionSeriesQueryParameters(
            CompanyId: _tenantContext.CompanyId,
            ConsumedMovementType: ConsumedMovementType,
            Bucket: ToDateTruncUnit(ResolveBucket(request)),
            From: request.From,
            To: request.To,
            PreviousFrom: previousFrom,
            PreviousTo: previousTo,
            ExperimentId: request.ExperimentId);
    }

    /// <summary>The bucket the caller pinned, or the one derived from the window width when none was given.</summary>
    internal static ConsumptionBucket ResolveBucket(GetConsumptionSeriesQuery request)
        => request.Bucket ?? DeriveBucket(request.From, request.To);

    /// <summary>
    /// Derives the granularity from the window width: up to ~2 months (62 days inclusive) is daily — covering
    /// the approved 7-day and 30-day views — and anything wider is monthly, covering the 3-month view. The
    /// threshold sits above 30 and below 90 so the three approved windows land on the approved buckets.
    /// </summary>
    internal static ConsumptionBucket DeriveBucket(DateOnly from, DateOnly to)
    {
        const int dailyWindowMaxDays = 62;
        int windowDays = InclusiveDays(from, to);
        return windowDays <= dailyWindowMaxDays ? ConsumptionBucket.Day : ConsumptionBucket.Month;
    }

    /// <summary>
    /// The same-length period immediately before <c>[from, to]</c>: a window of the same inclusive day count
    /// ending the day before <paramref name="from"/>. For a 30-day window it is the previous 30 days.
    /// </summary>
    internal static (DateOnly PreviousFrom, DateOnly PreviousTo) PreviousWindow(DateOnly from, DateOnly to)
    {
        int windowDays = InclusiveDays(from, to);
        DateOnly previousTo = from.AddDays(-1);
        DateOnly previousFrom = previousTo.AddDays(-(windowDays - 1));
        return (previousFrom, previousTo);
    }

    /// <summary>Inclusive day count of <c>[from, to]</c> (a single-day window is 1, never 0 or negative).</summary>
    private static int InclusiveDays(DateOnly from, DateOnly to)
        => Math.Max(1, to.DayNumber - from.DayNumber + 1);

    /// <summary>Maps the enum to the PostgreSQL <c>date_trunc</c> field literal.</summary>
    private static string ToDateTruncUnit(ConsumptionBucket bucket)
        => bucket == ConsumptionBucket.Month ? "month" : "day";

    /// <summary>
    /// Dapper materialization row for the totals result set: the current and previous per-unit totals, turned
    /// into a <see cref="ConsumptionPeriodTotal"/> with the computed delta.
    /// </summary>
    private sealed record PeriodTotalRow(string Unit, decimal CurrentTotal, decimal PreviousTotal)
    {
        public ConsumptionPeriodTotal ToPeriodTotal() => new(
            Unit,
            CurrentTotal,
            PreviousTotal,
            ConsumptionDelta.Compute(CurrentTotal, PreviousTotal));
    }
}

/// <summary>
/// Computes the signed percentage change of a period total versus the preceding period of the same length.
/// Isolated (pure, side-effect-free) so the rule is unit-tested directly.
/// </summary>
public static class ConsumptionDelta
{
    /// <summary>
    /// The percentage change from <paramref name="previous"/> to <paramref name="current"/>:
    /// <c>(current - previous) / previous * 100</c>. Returns null when <paramref name="previous"/> is zero —
    /// the change is undefined with no base (the UI shows "novo"/"—" rather than a fabricated +100%).
    /// </summary>
    public static decimal? Compute(decimal current, decimal previous)
        => previous == 0m ? null : (current - previous) / previous * 100m;
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="GetConsumptionSeriesQuery"/>. The property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the
/// tenant guard, the resolved bucket and the previous-window bounds can be asserted without a live database.
/// </summary>
internal sealed record ConsumptionSeriesQueryParameters(
    Guid CompanyId,
    string ConsumedMovementType,
    string Bucket,
    DateOnly From,
    DateOnly To,
    DateOnly PreviousFrom,
    DateOnly PreviousTo,
    Guid? ExperimentId);
