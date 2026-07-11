using SISLAB.Modules.Inventory.Domain.Equipments;
using SISLAB.Modules.Inventory.Tests.Domain.ValueObjects;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Domain.Equipments;

public sealed class CalibrationScheduleTests
{
    [Fact]
    public void Create_keeps_the_last_and_next_dates()
    {
        var schedule = CalibrationSchedule.Create(new DateOnly(2026, 1, 15), new DateOnly(2026, 7, 15));

        Assert.Equal(new DateOnly(2026, 1, 15), schedule.LastCalibration);
        Assert.Equal(new DateOnly(2026, 7, 15), schedule.NextCalibration);
    }

    [Fact]
    public void Create_allows_a_schedule_without_a_next_date()
    {
        var schedule = CalibrationSchedule.Create(new DateOnly(2026, 1, 15));

        Assert.Null(schedule.NextCalibration);
    }

    [Fact]
    public void Create_rejects_a_next_date_before_the_last()
        => Assert.Throws<DomainException>(() =>
            CalibrationSchedule.Create(new DateOnly(2026, 7, 1), new DateOnly(2026, 6, 1)));

    [Fact]
    public void Create_allows_a_next_date_equal_to_the_last()
    {
        var schedule = CalibrationSchedule.Create(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1));

        Assert.Equal(schedule.LastCalibration, schedule.NextCalibration);
    }

    [Fact]
    public void IsOverdue_is_false_without_a_next_date()
    {
        var schedule = CalibrationSchedule.Create(new DateOnly(2020, 1, 1));

        Assert.False(schedule.IsOverdue(FixedClock.On(2026, 7, 11)));
    }

    [Fact]
    public void IsOverdue_is_true_when_next_is_before_today()
    {
        var schedule = CalibrationSchedule.Create(new DateOnly(2025, 1, 1), new DateOnly(2026, 6, 1));

        Assert.True(schedule.IsOverdue(FixedClock.On(2026, 7, 11)));
    }

    [Fact]
    public void IsOverdue_is_false_when_next_is_today()
    {
        var schedule = CalibrationSchedule.Create(new DateOnly(2025, 1, 1), new DateOnly(2026, 7, 11));

        Assert.False(schedule.IsOverdue(FixedClock.On(2026, 7, 11)));
    }

    [Fact]
    public void Schedules_with_equal_dates_are_equal()
    {
        var a = CalibrationSchedule.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 1));
        var b = CalibrationSchedule.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 1));

        Assert.Equal(a, b);
    }
}
