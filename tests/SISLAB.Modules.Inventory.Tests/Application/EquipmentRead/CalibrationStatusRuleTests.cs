using SISLAB.Modules.Inventory.Application.Equipments.Queries;

namespace SISLAB.Modules.Inventory.Tests.Application.EquipmentRead;

/// <summary>
/// Covers the read-side calibration classification (card [E4] #27). <see cref="CalibrationStatusRule"/> is the
/// single specification the equipment-listing/detail SQL mirrors, so these tests lock its boundary conditions —
/// keeping the C# read model and the SQL CASE in lockstep without a live database. (The SQL itself is exercised
/// against PostgreSQL by the tenant-isolation integration test when Docker is available.)
/// </summary>
public sealed class CalibrationStatusRuleTests
{
    private static readonly DateOnly Today = new(2026, 7, 12);

    [Fact]
    public void No_planned_next_calibration_is_not_required()
        => Assert.Equal(CalibrationStatus.NotRequired, CalibrationStatusRule.Classify(null, Today));

    [Fact]
    public void Next_calibration_in_the_past_is_overdue()
        => Assert.Equal(CalibrationStatus.Overdue, CalibrationStatusRule.Classify(Today.AddDays(-1), Today));

    [Fact]
    public void Next_calibration_today_is_due_soon()
        => Assert.Equal(CalibrationStatus.DueSoon, CalibrationStatusRule.Classify(Today, Today));

    [Fact]
    public void Next_calibration_on_the_window_edge_is_due_soon()
    {
        DateOnly onEdge = Today.AddDays(CalibrationStatusRule.DefaultDueSoonWindowDays);

        Assert.Equal(CalibrationStatus.DueSoon, CalibrationStatusRule.Classify(onEdge, Today));
    }

    [Fact]
    public void Next_calibration_just_past_the_window_is_up_to_date()
    {
        DateOnly pastWindow = Today.AddDays(CalibrationStatusRule.DefaultDueSoonWindowDays + 1);

        Assert.Equal(CalibrationStatus.UpToDate, CalibrationStatusRule.Classify(pastWindow, Today));
    }

    [Fact]
    public void Next_calibration_well_in_the_future_is_up_to_date()
        => Assert.Equal(CalibrationStatus.UpToDate, CalibrationStatusRule.Classify(Today.AddDays(365), Today));

    [Fact]
    public void A_custom_window_widens_the_due_soon_band()
    {
        DateOnly next = Today.AddDays(45);

        Assert.Equal(CalibrationStatus.UpToDate, CalibrationStatusRule.Classify(next, Today));
        Assert.Equal(CalibrationStatus.DueSoon, CalibrationStatusRule.Classify(next, Today, dueSoonWindowDays: 60));
    }
}
