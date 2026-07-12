using SISLAB.Modules.Inventory.Application.StockRead;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Tests.Application.StockRead;

/// <summary>
/// Covers the tenant guard and filter normalization of the E4 #29 read-side query handlers, without a live
/// database: the Dapper parameter set they build is asserted directly. The mandatory
/// <c>WHERE company_id = @CompanyId</c> is only as safe as its parameter, so the tests pin that the company
/// id always comes from <see cref="ITenantContext"/> (never the request) and that blank filters collapse to
/// null. The SQL body itself was validated against PostgreSQL by a smoke test.
/// </summary>
public sealed class StockReadQueryParametersTests
{
    private static readonly Guid ActiveCompany = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime Now = new(2026, 7, 12, 8, 0, 0, DateTimeKind.Utc);

    // BuildParameters does not touch the connection factory, so a null factory is never dereferenced here.
    private readonly ListStockItemsQueryHandler _itemsHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany), new FixedClock(Now));

    private readonly GetLocationsSummaryQueryHandler _summaryHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany), new FixedClock(Now));

    private readonly ListExpiringItemsQueryHandler _expiringHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany), new FixedClock(Now));

    private readonly GetExpirySummaryQueryHandler _expirySummaryHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany), new FixedClock(Now));

    private readonly ListItemsBelowMinimumQueryHandler _belowMinimumHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany));

    private readonly GetBelowMinimumSummaryQueryHandler _belowMinimumSummaryHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany));

    private readonly GetConsumptionReportQueryHandler _consumptionReportHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany));

    private readonly GetConsumptionSeriesQueryHandler _consumptionSeriesHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany));

    private static readonly DateOnly WindowFrom = new(2026, 6, 1);
    private static readonly DateOnly WindowTo = new(2026, 6, 30);

    [Fact]
    public void List_query_takes_the_company_from_the_tenant_context()
    {
        StockItemsQueryParameters parameters = _itemsHandler.BuildParameters(new ListStockItemsQuery());

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }

    [Fact]
    public void List_query_derives_today_from_the_clock()
    {
        StockItemsQueryParameters parameters = _itemsHandler.BuildParameters(new ListStockItemsQuery());

        Assert.Equal(DateOnly.FromDateTime(Now), parameters.Today);
        Assert.Equal(ExpectedWarningWindowDays, parameters.WarningWindowDays);
    }

    [Fact]
    public void List_query_carries_the_expiry_status_ordinals_for_the_sql_case()
    {
        StockItemsQueryParameters parameters = _itemsHandler.BuildParameters(new ListStockItemsQuery());

        Assert.Equal((int)ExpiryStatusView.NotApplicable, parameters.NotApplicable);
        Assert.Equal((int)ExpiryStatusView.Ok, parameters.Ok);
        Assert.Equal((int)ExpiryStatusView.ExpiringSoon, parameters.ExpiringSoon);
        Assert.Equal((int)ExpiryStatusView.Expired, parameters.Expired);
    }

    [Fact]
    public void List_query_maps_pagination_bounds()
    {
        StockItemsQueryParameters parameters =
            _itemsHandler.BuildParameters(new ListStockItemsQuery { Page = 3, PageSize = 25 });

        Assert.Equal(51, parameters.FirstResult); // (3-1)*25 + 1
        Assert.Equal(75, parameters.LastResult);  // 3*25
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void List_query_collapses_blank_filters_to_null(string? blank)
    {
        StockItemsQueryParameters parameters = _itemsHandler.BuildParameters(
            new ListStockItemsQuery { Category = blank, Search = blank });

        Assert.Null(parameters.Category);
        Assert.Null(parameters.Search);
    }

    [Fact]
    public void List_query_trims_populated_filters()
    {
        StockItemsQueryParameters parameters = _itemsHandler.BuildParameters(
            new ListStockItemsQuery { Category = "  Solvent  ", Search = "  eta  " });

        Assert.Equal("Solvent", parameters.Category);
        Assert.Equal("eta", parameters.Search);
    }

    [Fact]
    public void List_query_passes_the_storage_location_filter_through()
    {
        Guid location = Guid.NewGuid();

        StockItemsQueryParameters parameters =
            _itemsHandler.BuildParameters(new ListStockItemsQuery { StorageLocationId = location });

        Assert.Equal(location, parameters.StorageLocationId);
    }

    [Fact]
    public void Summary_query_takes_the_company_from_the_tenant_context()
    {
        LocationsSummaryQueryParameters parameters = _summaryHandler.BuildParameters();

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }

    [Fact]
    public void Summary_query_derives_today_from_the_clock_and_flags_controlled_storage()
    {
        LocationsSummaryQueryParameters parameters = _summaryHandler.BuildParameters();

        Assert.Equal(DateOnly.FromDateTime(Now), parameters.Today);
        Assert.Equal("Controlled", parameters.ControlledType);
    }

    [Fact]
    public void Expiring_query_takes_the_company_from_the_tenant_context()
    {
        ExpiringItemsQueryParameters parameters = _expiringHandler.BuildParameters(new ListExpiringItemsQuery());

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }

    [Fact]
    public void Expiring_query_derives_today_and_defaults_to_the_shared_window_including_expired()
    {
        ExpiringItemsQueryParameters parameters = _expiringHandler.BuildParameters(new ListExpiringItemsQuery());

        Assert.Equal(DateOnly.FromDateTime(Now), parameters.Today);
        Assert.Equal(ExpectedWarningWindowDays, parameters.WarningWindowDays);
        Assert.True(parameters.IncludeExpired);
    }

    [Fact]
    public void Expiring_query_carries_the_status_ordinals_for_the_sql_filter()
    {
        ExpiringItemsQueryParameters parameters = _expiringHandler.BuildParameters(new ListExpiringItemsQuery());

        Assert.Equal((int)ExpiryStatusView.Ok, parameters.Ok);
        Assert.Equal((int)ExpiryStatusView.ExpiringSoon, parameters.ExpiringSoon);
        Assert.Equal((int)ExpiryStatusView.Expired, parameters.Expired);
    }

    [Fact]
    public void Expiring_query_passes_a_positive_window_and_the_expired_flag_through()
    {
        ExpiringItemsQueryParameters parameters = _expiringHandler.BuildParameters(
            new ListExpiringItemsQuery { WarningWindowDays = 7, IncludeExpired = false });

        Assert.Equal(7, parameters.WarningWindowDays);
        Assert.False(parameters.IncludeExpired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Expiring_query_collapses_a_non_positive_window_to_the_shared_default(int window)
    {
        ExpiringItemsQueryParameters parameters = _expiringHandler.BuildParameters(
            new ListExpiringItemsQuery { WarningWindowDays = window });

        Assert.Equal(ExpectedWarningWindowDays, parameters.WarningWindowDays);
    }

    [Fact]
    public void Expiring_query_maps_pagination_bounds()
    {
        ExpiringItemsQueryParameters parameters =
            _expiringHandler.BuildParameters(new ListExpiringItemsQuery { Page = 2, PageSize = 50 });

        Assert.Equal(51, parameters.FirstResult); // (2-1)*50 + 1
        Assert.Equal(100, parameters.LastResult); // 2*50
    }

    [Fact]
    public void Expiring_query_passes_the_storage_location_filter_through()
    {
        Guid location = Guid.NewGuid();

        ExpiringItemsQueryParameters parameters =
            _expiringHandler.BuildParameters(new ListExpiringItemsQuery { StorageLocationId = location });

        Assert.Equal(location, parameters.StorageLocationId);
    }

    [Fact]
    public void Expiry_summary_query_takes_the_company_from_the_tenant_context()
    {
        ExpirySummaryQueryParameters parameters = _expirySummaryHandler.BuildParameters();

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }

    [Fact]
    public void Expiry_summary_query_derives_today_and_uses_the_shared_window()
    {
        ExpirySummaryQueryParameters parameters = _expirySummaryHandler.BuildParameters();

        Assert.Equal(DateOnly.FromDateTime(Now), parameters.Today);
        Assert.Equal(ExpectedWarningWindowDays, parameters.WarningWindowDays);
    }

    [Fact]
    public void Below_minimum_query_takes_the_company_from_the_tenant_context()
    {
        BelowMinimumQueryParameters parameters =
            _belowMinimumHandler.BuildParameters(new ListItemsBelowMinimumQuery());

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }

    [Fact]
    public void Below_minimum_query_passes_the_storage_location_filter_through()
    {
        Guid location = Guid.NewGuid();

        BelowMinimumQueryParameters parameters =
            _belowMinimumHandler.BuildParameters(new ListItemsBelowMinimumQuery { StorageLocationId = location });

        Assert.Equal(location, parameters.StorageLocationId);
    }

    [Fact]
    public void Below_minimum_query_leaves_the_storage_location_filter_null_by_default()
    {
        BelowMinimumQueryParameters parameters =
            _belowMinimumHandler.BuildParameters(new ListItemsBelowMinimumQuery());

        Assert.Null(parameters.StorageLocationId);
    }

    [Fact]
    public void Below_minimum_query_maps_pagination_bounds()
    {
        BelowMinimumQueryParameters parameters =
            _belowMinimumHandler.BuildParameters(new ListItemsBelowMinimumQuery { Page = 4, PageSize = 10 });

        Assert.Equal(31, parameters.FirstResult); // (4-1)*10 + 1
        Assert.Equal(40, parameters.LastResult);  // 4*10
    }

    [Fact]
    public void Below_minimum_summary_query_takes_the_company_from_the_tenant_context()
    {
        BelowMinimumSummaryQueryParameters parameters = _belowMinimumSummaryHandler.BuildParameters();

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }

    // --- Consumption report (card [E4] #31) ------------------------------------------------------------

    [Fact]
    public void Consumption_report_query_takes_the_company_from_the_tenant_context()
    {
        ConsumptionReportQueryParameters parameters = _consumptionReportHandler.BuildParameters(ValidReport());

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }

    [Fact]
    public void Consumption_report_query_pins_the_consumed_movement_type_and_window()
    {
        ConsumptionReportQueryParameters parameters = _consumptionReportHandler.BuildParameters(ValidReport());

        Assert.Equal("Consumed", parameters.ConsumedMovementType);
        Assert.Equal(WindowFrom, parameters.From);
        Assert.Equal(WindowTo, parameters.To);
    }

    [Fact]
    public void Consumption_report_query_passes_the_experiment_filter_through()
    {
        Guid experiment = Guid.NewGuid();

        ConsumptionReportQueryParameters parameters =
            _consumptionReportHandler.BuildParameters(ValidReport() with { ExperimentId = experiment });

        Assert.Equal(experiment, parameters.ExperimentId);
    }

    [Fact]
    public void Consumption_report_query_leaves_the_experiment_filter_null_by_default()
    {
        ConsumptionReportQueryParameters parameters = _consumptionReportHandler.BuildParameters(ValidReport());

        Assert.Null(parameters.ExperimentId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Consumption_report_query_collapses_a_blank_category_to_null(string? blank)
    {
        ConsumptionReportQueryParameters parameters =
            _consumptionReportHandler.BuildParameters(ValidReport() with { Category = blank });

        Assert.Null(parameters.Category);
    }

    [Fact]
    public void Consumption_report_query_trims_a_populated_category()
    {
        ConsumptionReportQueryParameters parameters =
            _consumptionReportHandler.BuildParameters(ValidReport() with { Category = "  Solvent  " });

        Assert.Equal("Solvent", parameters.Category);
    }

    [Fact]
    public void Consumption_report_query_maps_pagination_bounds()
    {
        ConsumptionReportQueryParameters parameters =
            _consumptionReportHandler.BuildParameters(ValidReport() with { Page = 2, PageSize = 15 });

        Assert.Equal(16, parameters.FirstResult); // (2-1)*15 + 1
        Assert.Equal(30, parameters.LastResult);  // 2*15
    }

    [Fact]
    public void Consumption_report_query_rejects_an_unset_window()
    {
        Assert.Throws<BusinessException>(() =>
            _consumptionReportHandler.BuildParameters(new GetConsumptionReportQuery()));
    }

    [Fact]
    public void Consumption_report_query_rejects_an_inverted_window()
    {
        Assert.Throws<BusinessException>(() =>
            _consumptionReportHandler.BuildParameters(
                new GetConsumptionReportQuery { From = WindowTo, To = WindowFrom }));
    }

    // --- Consumption series (card [E4] #31) ------------------------------------------------------------

    [Fact]
    public void Consumption_series_query_takes_the_company_from_the_tenant_context()
    {
        ConsumptionSeriesQueryParameters parameters = _consumptionSeriesHandler.BuildParameters(ValidSeries());

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }

    [Fact]
    public void Consumption_series_query_pins_the_consumed_movement_type_and_window()
    {
        ConsumptionSeriesQueryParameters parameters = _consumptionSeriesHandler.BuildParameters(ValidSeries());

        Assert.Equal("Consumed", parameters.ConsumedMovementType);
        Assert.Equal(WindowFrom, parameters.From);
        Assert.Equal(WindowTo, parameters.To);
    }

    [Fact]
    public void Consumption_series_query_maps_the_previous_window_to_the_same_length_before_the_current_one()
    {
        // Current window is June (30 days); the previous same-length window is the 30 days ending May 31.
        ConsumptionSeriesQueryParameters parameters = _consumptionSeriesHandler.BuildParameters(ValidSeries());

        Assert.Equal(new DateOnly(2026, 5, 31), parameters.PreviousTo);
        Assert.Equal(new DateOnly(2026, 5, 2), parameters.PreviousFrom); // 30 inclusive days ending May 31
    }

    [Fact]
    public void Consumption_series_query_derives_a_daily_bucket_for_a_thirty_day_window()
    {
        ConsumptionSeriesQueryParameters parameters = _consumptionSeriesHandler.BuildParameters(ValidSeries());

        Assert.Equal("day", parameters.Bucket);
    }

    [Fact]
    public void Consumption_series_query_derives_a_monthly_bucket_for_a_three_month_window()
    {
        var query = new GetConsumptionSeriesQuery
        {
            From = new DateOnly(2026, 4, 1),
            To = new DateOnly(2026, 6, 30)
        };

        ConsumptionSeriesQueryParameters parameters = _consumptionSeriesHandler.BuildParameters(query);

        Assert.Equal("month", parameters.Bucket);
    }

    [Fact]
    public void Consumption_series_query_honours_an_explicit_bucket_over_the_derived_one()
    {
        // A 30-day window would derive Day; an explicit Month must win.
        ConsumptionSeriesQueryParameters parameters =
            _consumptionSeriesHandler.BuildParameters(ValidSeries() with { Bucket = ConsumptionBucket.Month });

        Assert.Equal("month", parameters.Bucket);
    }

    [Fact]
    public void Consumption_series_query_passes_the_experiment_filter_through()
    {
        Guid experiment = Guid.NewGuid();

        ConsumptionSeriesQueryParameters parameters =
            _consumptionSeriesHandler.BuildParameters(ValidSeries() with { ExperimentId = experiment });

        Assert.Equal(experiment, parameters.ExperimentId);
    }

    [Fact]
    public void Consumption_series_query_rejects_an_unset_window()
    {
        Assert.Throws<BusinessException>(() =>
            _consumptionSeriesHandler.BuildParameters(new GetConsumptionSeriesQuery()));
    }

    [Fact]
    public void Consumption_series_query_rejects_an_inverted_window()
    {
        Assert.Throws<BusinessException>(() =>
            _consumptionSeriesHandler.BuildParameters(
                new GetConsumptionSeriesQuery { From = WindowTo, To = WindowFrom }));
    }

    private static GetConsumptionReportQuery ValidReport() =>
        new() { From = WindowFrom, To = WindowTo };

    private static GetConsumptionSeriesQuery ValidSeries() =>
        new() { From = WindowFrom, To = WindowTo };

    private const int ExpectedWarningWindowDays = 30;

    private sealed class StubTenantContext : ITenantContext
    {
        public StubTenantContext(Guid companyId) => CompanyId = companyId;

        public Guid CompanyId { get; }
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; }
    }
}
