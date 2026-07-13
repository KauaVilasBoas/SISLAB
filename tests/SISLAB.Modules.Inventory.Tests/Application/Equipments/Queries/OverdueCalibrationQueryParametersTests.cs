using SISLAB.Modules.Inventory.Application.Equipments.Queries;

namespace SISLAB.Modules.Inventory.Tests.Application.Equipments.Queries;

/// <summary>
/// Covers the tenant guard, the SQL-derived status ordinal and the clock-sourced <c>@Today</c> of the E6 #66
/// overdue-calibration read query — without a live database, by asserting the Dapper parameter set the handler
/// builds. The mandatory <c>WHERE company_id = @CompanyId</c> is only as safe as its parameter, so the tests
/// pin that the company id always comes from <see cref="ITenantContext"/> (never the request), that
/// <c>@Today</c> comes from <see cref="IClock"/>, and that the Overdue status ordinal the SQL projects matches
/// the enum. The SQL body itself (NULL next_calibration ignored, days-overdue subtraction) is validated by the
/// static SQL guard and the PostgreSQL dialect conventions.
/// </summary>
public sealed class OverdueCalibrationQueryParametersTests
{
    private static readonly Guid ActiveCompany = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime Now = new(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);

    // BuildParameters never touches the connection factory, so a null factory is safe here.
    private readonly ListOverdueCalibrationEquipmentQueryHandler _handler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany), new FixedClock(Now));

    [Fact]
    public void Takes_the_company_from_the_tenant_context_never_the_request()
    {
        OverdueCalibrationQueryParameters parameters =
            _handler.BuildParameters(new ListOverdueCalibrationEquipmentQuery());

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }

    [Fact]
    public void Derives_today_from_the_clock()
    {
        OverdueCalibrationQueryParameters parameters =
            _handler.BuildParameters(new ListOverdueCalibrationEquipmentQuery());

        Assert.Equal(DateOnly.FromDateTime(Now), parameters.Today);
    }

    [Fact]
    public void Carries_the_overdue_status_ordinal_the_sql_projects()
    {
        OverdueCalibrationQueryParameters parameters =
            _handler.BuildParameters(new ListOverdueCalibrationEquipmentQuery());

        Assert.Equal((int)CalibrationStatusView.Overdue, parameters.Overdue);
    }

    [Fact]
    public void Honours_pagination_bounds_from_the_query()
    {
        OverdueCalibrationQueryParameters parameters =
            _handler.BuildParameters(new ListOverdueCalibrationEquipmentQuery { Page = 2, PageSize = 50 });

        Assert.Equal(51, parameters.FirstResult);
        Assert.Equal(100, parameters.LastResult);
    }
}
