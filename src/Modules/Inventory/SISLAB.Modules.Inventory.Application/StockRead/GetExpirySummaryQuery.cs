using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Application.StockRead;

/// <summary>
/// Read-side query (card [E4] #30) that aggregates, for the <b>active company</b>, the three counts of the
/// dashboard "Situação de validade" donut (#49): <b>Vencidos</b>, <b>Vencem ≤30d</b> and <b>Em dia</b>. It
/// counts the stock items of <c>inventory.stock_view</c> by their derived expiry status via Dapper, in a
/// single tenant-scoped round-trip.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three slices, not four.</b> The donut has exactly three slices — items with no recorded validity
/// (<see cref="ExpiryStatusView.NotApplicable"/>) are not a slice and are excluded from every count, so the
/// three totals cover only items that carry a validity. The window is the shared 30-day one
/// (<see cref="ExpiryStatusRule.DefaultWarningWindowDays"/>), matching the "≤30d" label; the classification
/// is a faithful mirror of <see cref="ExpiryStatusRule"/>.
/// </para>
/// <para>
/// <b>Derived, never persisted.</b> The status is computed in SQL against the handler-supplied <c>@Today</c>
/// (from <see cref="IClock"/>), never the database clock, using the same last-valid-day rule as the item
/// listings (an item is valid through the last day of its expiry month).
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from
/// the request, and the single SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF
/// global query filter, so the tenant guard is explicit (defense-in-depth, section 7).
/// </para>
/// </remarks>
public sealed record GetExpirySummaryQuery : IQuery<ExpirySummary>;

/// <summary>
/// The three donut totals for the active company (card [E4] #30). Counts only items carrying a validity;
/// <see cref="Total"/> is their sum (the number of items with recorded validity), so the UI can render slice
/// percentages without a second query.
/// </summary>
public sealed record ExpirySummary(int Expired, int ExpiringSoon, int Ok)
{
    /// <summary>Items with a recorded validity — the sum of the three slices (the donut's whole).</summary>
    public int Total => Expired + ExpiringSoon + Ok;
}

internal sealed class GetExpirySummaryQueryHandler
    : BaseDataAccess, IQueryHandler<GetExpirySummaryQuery, ExpirySummary>
{
    // Each COUNT ... FILTER counts items in one status band, mirroring ExpiryStatusRule.Classify against the
    // last valid day of (expiry_year, expiry_month): Expired once @Today is past it, ExpiringSoon when it is
    // within the 30-day window from @Today, Ok otherwise. Items with no validity (NULL year/month) match no
    // filter, so they fall outside all three slices — the donut has three slices, not four. @Today and
    // @WarningWindowDays come from the handler (IClock), never the DB clock. A single row always comes back.
    private const string Sql =
        """
        SELECT
            COUNT(*) FILTER (
                WHERE v.expiry_year IS NOT NULL
                  AND v.expiry_month IS NOT NULL
                  AND @Today > (make_date(v.expiry_year, v.expiry_month, 1)
                                + INTERVAL '1 month' - INTERVAL '1 day')::date
            ) AS expired,
            COUNT(*) FILTER (
                WHERE v.expiry_year IS NOT NULL
                  AND v.expiry_month IS NOT NULL
                  AND @Today <= (make_date(v.expiry_year, v.expiry_month, 1)
                                 + INTERVAL '1 month' - INTERVAL '1 day')::date
                  AND (make_date(v.expiry_year, v.expiry_month, 1)
                       + INTERVAL '1 month' - INTERVAL '1 day')::date
                      <= (@Today + (@WarningWindowDays || ' days')::interval)::date
            ) AS expiringsoon,
            COUNT(*) FILTER (
                WHERE v.expiry_year IS NOT NULL
                  AND v.expiry_month IS NOT NULL
                  AND (make_date(v.expiry_year, v.expiry_month, 1)
                       + INTERVAL '1 month' - INTERVAL '1 day')::date
                      > (@Today + (@WarningWindowDays || ' days')::interval)::date
            ) AS ok
        FROM inventory.stock_view AS v
        WHERE v.company_id = @CompanyId;
        """;

    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public GetExpirySummaryQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext,
        IClock clock)
        : base(connectionFactory)
    {
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<ExpirySummary> HandleAsync(
        GetExpirySummaryQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        ExpirySummaryQueryParameters parameters = BuildParameters();

        return await connection.QuerySingleAsync<ExpirySummary>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Materializes the Dapper parameter set: the company id always comes from <see cref="ITenantContext"/>
    /// (never the request), <c>@Today</c> from the injected <see cref="IClock"/> and the window from the shared
    /// 30-day default. Extracted so the tenant guard is unit-testable without a live database.
    /// </summary>
    internal ExpirySummaryQueryParameters BuildParameters() => new(
        CompanyId: _tenantContext.CompanyId,
        Today: DateOnly.FromDateTime(_clock.UtcNow),
        WarningWindowDays: ExpiryStatusRule.DefaultWarningWindowDays);
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="GetExpirySummaryQuery"/>. Property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the
/// tenant guard can be asserted without a live database.
/// </summary>
internal sealed record ExpirySummaryQueryParameters(
    Guid CompanyId,
    DateOnly Today,
    int WarningWindowDays);
