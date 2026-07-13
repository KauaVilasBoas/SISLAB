using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SISLAB.Jobs.Configuration;
using SISLAB.Jobs.Jobs;
using SISLAB.Jobs.Tests.Jobs.TestSupport;
using SISLAB.Modules.Inventory.Application.Equipments.Queries;
using SISLAB.Modules.Notifications.Contracts;

namespace SISLAB.Jobs.Tests.Jobs;

/// <summary>
/// End-to-end tick tests for the overdue-calibration alert job (#66): produced notifications, cross-tenant
/// isolation and resilience — driven through the real job over the in-memory host.
/// </summary>
public sealed class CalibrationOverdueAlertJobTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();

    [Fact]
    public async Task Raises_calibration_notifications_per_company()
    {
        Guid eqA = Guid.NewGuid();
        Guid eqB = Guid.NewGuid();

        AlertJobTestHost<OverdueCalibrationEquipment> host = new(
            companyIds: [CompanyA, CompanyB],
            rowsByCompany: new Dictionary<Guid, IReadOnlyList<OverdueCalibrationEquipment>>
            {
                [CompanyA] = [Equipment(eqA, daysOverdue: 5)],
                [CompanyB] = [Equipment(eqB, daysOverdue: 20)]
            });

        await RunOneTickAsync(host, expectedRaises: 2);

        Assert.All(host.Publisher.Raised, r =>
        {
            Assert.Equal(NotificationTypeCode.Calibration, r.Type);
            Assert.Equal(NotificationSeverityLevel.Warning, r.Severity);
            Assert.Equal("equipment", r.TargetType);
        });
        Assert.Contains(host.Publisher.Raised, r => r.TargetId == eqA);
        Assert.Contains(host.Publisher.Raised, r => r.TargetId == eqB);
    }

    [Fact]
    public async Task Does_not_leak_across_tenants()
    {
        Guid eqB = Guid.NewGuid();

        AlertJobTestHost<OverdueCalibrationEquipment> host = new(
            companyIds: [CompanyA, CompanyB],
            rowsByCompany: new Dictionary<Guid, IReadOnlyList<OverdueCalibrationEquipment>>
            {
                [CompanyA] = [],
                [CompanyB] = [Equipment(eqB, daysOverdue: 8)]
            });

        await RunOneTickAsync(host, expectedRaises: 1);

        Assert.Single(host.Publisher.Raised);
        Assert.Equal(eqB, host.Publisher.Raised[0].TargetId);
    }

    [Fact]
    public async Task A_failing_company_does_not_stop_the_others()
    {
        Guid eqB = Guid.NewGuid();

        AlertJobTestHost<OverdueCalibrationEquipment> host = new(
            companyIds: [CompanyA, CompanyB],
            rowsByCompany: new Dictionary<Guid, IReadOnlyList<OverdueCalibrationEquipment>>
            {
                [CompanyA] = [Equipment(Guid.NewGuid(), daysOverdue: 3)],
                [CompanyB] = [Equipment(eqB, daysOverdue: 3)]
            },
            failingCompanies: [CompanyA]);

        await RunOneTickAsync(host, expectedRaises: 1);

        Assert.Single(host.Publisher.Raised);
        Assert.Equal(eqB, host.Publisher.Raised[0].TargetId);
    }

    private static async Task RunOneTickAsync(AlertJobTestHost<OverdueCalibrationEquipment> host, int expectedRaises)
    {
        JobsOptions options = new()
        {
            CalibrationAlert = new CalibrationAlertOptions { Interval = TimeSpan.FromMinutes(10) }
        };
        CalibrationOverdueAlertJob job = new(
            host, Options.Create(options), host.Clock, NullLogger<CalibrationOverdueAlertJob>.Instance);

        await job.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => host.Publisher.Raised.Count >= expectedRaises);
        await job.StopAsync(CancellationToken.None);
    }

    private static OverdueCalibrationEquipment Equipment(Guid id, int daysOverdue) => new(
        Id: id,
        Name: "Balança",
        AssetTag: "EQ-100",
        Brand: "Shimadzu",
        Model: "X",
        LastCalibration: new DateOnly(2025, 1, 1),
        NextCalibration: new DateOnly(2026, 7, 13).AddDays(-daysOverdue),
        CalibrationStatus: CalibrationStatusView.Overdue,
        DaysOverdue: daysOverdue,
        StorageLocationId: Guid.NewGuid());

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource cts = new(TestTimeout);
        while (!condition())
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, cts.Token);
        }
    }
}
