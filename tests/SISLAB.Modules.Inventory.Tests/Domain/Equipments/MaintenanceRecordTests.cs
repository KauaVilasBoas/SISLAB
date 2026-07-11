using SISLAB.Modules.Inventory.Domain.Equipments;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Domain.Equipments;

public sealed class MaintenanceRecordTests
{
    [Fact]
    public void Create_keeps_the_date_type_and_normalized_notes()
    {
        var record = MaintenanceRecord.Create(
            new DateOnly(2026, 3, 10), MaintenanceType.Corrective, "  Troca de fusível  ");

        Assert.Equal(new DateOnly(2026, 3, 10), record.Date);
        Assert.Equal(MaintenanceType.Corrective, record.Type);
        Assert.Equal("Troca de fusível", record.Notes);
    }

    [Fact]
    public void Create_treats_blank_notes_as_none()
    {
        var record = MaintenanceRecord.Create(new DateOnly(2026, 3, 10), MaintenanceType.Preventive, "   ");

        Assert.Null(record.Notes);
    }

    [Fact]
    public void Create_rejects_notes_over_the_maximum_length()
        => Assert.Throws<DomainException>(() => MaintenanceRecord.Create(
            new DateOnly(2026, 3, 10), MaintenanceType.Preventive, new string('x', 1001)));

    [Fact]
    public void Records_with_equal_components_are_equal()
    {
        var a = MaintenanceRecord.Create(new DateOnly(2026, 3, 10), MaintenanceType.Calibration, "ok");
        var b = MaintenanceRecord.Create(new DateOnly(2026, 3, 10), MaintenanceType.Calibration, "ok");

        Assert.Equal(a, b);
    }
}
