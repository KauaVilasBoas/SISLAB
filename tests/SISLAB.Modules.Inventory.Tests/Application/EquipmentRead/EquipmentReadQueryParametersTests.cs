using SISLAB.Modules.Inventory.Application.EquipmentRead;
using SISLAB.Modules.Inventory.Domain.Equipments;
using SISLAB.TestSupport;

namespace SISLAB.Modules.Inventory.Tests.Application.EquipmentRead;

/// <summary>
/// Covers the tenant guard, the derived-status ordinals and the filter normalization of the E4 #27 equipment
/// read-side query handlers, without a live database: the Dapper parameter set they build is asserted directly.
/// The mandatory <c>WHERE company_id = @CompanyId</c> is only as safe as its parameter, so these tests pin that
/// the company id always comes from <see cref="ITenantContext"/> (never the request), that <c>@Today</c> comes
/// from the clock, and that filters normalize as expected. The SQL body itself is validated against PostgreSQL by
/// the tenant-isolation integration test (when Docker is available).
/// </summary>
public sealed class EquipmentReadQueryParametersTests
{
    private static readonly Guid ActiveCompany = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime Now = new(2026, 7, 12, 8, 0, 0, DateTimeKind.Utc);
    private const int ExpectedDueSoonWindowDays = 30;

    // BuildParameters does not touch the connection factory, so a null factory is never dereferenced here.
    private readonly ListEquipmentQueryHandler _listHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany), new FixedClock(Now));

    private readonly GetEquipmentDetailQueryHandler _detailHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany), new FixedClock(Now));

    [Fact]
    public void List_query_takes_the_company_from_the_tenant_context()
    {
        EquipmentListQueryParameters parameters = _listHandler.BuildParameters(new ListEquipmentQuery());

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }

    [Fact]
    public void List_query_derives_today_from_the_clock_and_uses_the_default_window()
    {
        EquipmentListQueryParameters parameters = _listHandler.BuildParameters(new ListEquipmentQuery());

        Assert.Equal(DateOnly.FromDateTime(Now), parameters.Today);
        Assert.Equal(ExpectedDueSoonWindowDays, parameters.DueSoonWindowDays);
    }

    [Fact]
    public void List_query_carries_the_calibration_status_ordinals_for_the_sql_case()
    {
        EquipmentListQueryParameters parameters = _listHandler.BuildParameters(new ListEquipmentQuery());

        Assert.Equal((int)CalibrationStatus.NotRequired, parameters.NotRequired);
        Assert.Equal((int)CalibrationStatus.UpToDate, parameters.UpToDate);
        Assert.Equal((int)CalibrationStatus.DueSoon, parameters.DueSoon);
        Assert.Equal((int)CalibrationStatus.Overdue, parameters.Overdue);
    }

    [Fact]
    public void List_query_carries_the_inactive_status_name_for_the_active_derivation()
    {
        EquipmentListQueryParameters parameters = _listHandler.BuildParameters(new ListEquipmentQuery());

        Assert.Equal(EquipmentStatus.Inactive.ToString(), parameters.InactiveStatus);
    }

    [Fact]
    public void List_query_hides_inactive_equipment_by_default()
    {
        EquipmentListQueryParameters parameters = _listHandler.BuildParameters(new ListEquipmentQuery());

        Assert.False(parameters.IncludeInactive);
    }

    [Fact]
    public void List_query_passes_the_include_inactive_flag_through()
    {
        EquipmentListQueryParameters parameters =
            _listHandler.BuildParameters(new ListEquipmentQuery { IncludeInactive = true });

        Assert.True(parameters.IncludeInactive);
    }

    [Fact]
    public void List_query_leaves_the_status_filter_null_by_default()
    {
        EquipmentListQueryParameters parameters = _listHandler.BuildParameters(new ListEquipmentQuery());

        Assert.Null(parameters.Status);
    }

    [Theory]
    [InlineData(CalibrationStatus.NotRequired)]
    [InlineData(CalibrationStatus.UpToDate)]
    [InlineData(CalibrationStatus.DueSoon)]
    [InlineData(CalibrationStatus.Overdue)]
    public void List_query_maps_the_status_filter_to_its_ordinal(CalibrationStatus status)
    {
        EquipmentListQueryParameters parameters =
            _listHandler.BuildParameters(new ListEquipmentQuery { Status = status });

        Assert.Equal((int)status, parameters.Status);
    }

    [Fact]
    public void List_query_passes_the_storage_location_filter_through()
    {
        Guid location = Guid.NewGuid();

        EquipmentListQueryParameters parameters =
            _listHandler.BuildParameters(new ListEquipmentQuery { StorageLocationId = location });

        Assert.Equal(location, parameters.StorageLocationId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void List_query_collapses_a_blank_search_to_null(string? blank)
    {
        EquipmentListQueryParameters parameters =
            _listHandler.BuildParameters(new ListEquipmentQuery { Search = blank });

        Assert.Null(parameters.Search);
    }

    [Fact]
    public void List_query_trims_a_populated_search()
    {
        EquipmentListQueryParameters parameters =
            _listHandler.BuildParameters(new ListEquipmentQuery { Search = "  PAT-0041  " });

        Assert.Equal("PAT-0041", parameters.Search);
    }

    [Fact]
    public void List_query_maps_pagination_bounds()
    {
        EquipmentListQueryParameters parameters =
            _listHandler.BuildParameters(new ListEquipmentQuery { Page = 3, PageSize = 25 });

        Assert.Equal(51, parameters.FirstResult); // (3-1)*25 + 1
        Assert.Equal(75, parameters.LastResult);  // 3*25
    }

    [Fact]
    public void Detail_query_takes_the_company_from_the_tenant_context()
    {
        EquipmentDetailQueryParameters parameters =
            _detailHandler.BuildParameters(new GetEquipmentDetailQuery(Guid.NewGuid()));

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }

    [Fact]
    public void Detail_query_passes_the_requested_id_and_derives_today()
    {
        Guid equipmentId = Guid.NewGuid();

        EquipmentDetailQueryParameters parameters =
            _detailHandler.BuildParameters(new GetEquipmentDetailQuery(equipmentId));

        Assert.Equal(equipmentId, parameters.EquipmentId);
        Assert.Equal(DateOnly.FromDateTime(Now), parameters.Today);
        Assert.Equal(ExpectedDueSoonWindowDays, parameters.DueSoonWindowDays);
    }

    [Fact]
    public void Detail_query_carries_the_calibration_status_ordinals_and_inactive_name()
    {
        EquipmentDetailQueryParameters parameters =
            _detailHandler.BuildParameters(new GetEquipmentDetailQuery(Guid.NewGuid()));

        Assert.Equal((int)CalibrationStatus.NotRequired, parameters.NotRequired);
        Assert.Equal((int)CalibrationStatus.UpToDate, parameters.UpToDate);
        Assert.Equal((int)CalibrationStatus.DueSoon, parameters.DueSoon);
        Assert.Equal((int)CalibrationStatus.Overdue, parameters.Overdue);
        Assert.Equal(EquipmentStatus.Inactive.ToString(), parameters.InactiveStatus);
    }
}
