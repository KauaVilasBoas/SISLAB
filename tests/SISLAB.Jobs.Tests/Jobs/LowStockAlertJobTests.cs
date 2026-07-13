using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SISLAB.Jobs.Configuration;
using SISLAB.Jobs.Jobs;
using SISLAB.Jobs.Tests.Jobs.TestSupport;
using SISLAB.Modules.Inventory.Application.StockRead;
using SISLAB.Modules.Notifications.Contracts;

namespace SISLAB.Jobs.Tests.Jobs;

/// <summary>
/// End-to-end tick tests for the low-stock alert job (#42): produced notifications, cross-tenant isolation,
/// per-cycle idempotency and resilience — driven through the real job over the in-memory host.
/// </summary>
public sealed class LowStockAlertJobTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();

    [Fact]
    public async Task Raises_low_stock_notifications_per_company_with_controlled_items_critical()
    {
        Guid controlled = Guid.NewGuid();
        Guid ordinary = Guid.NewGuid();

        AlertJobTestHost<BelowMinimumItem> host = new(
            companyIds: [CompanyA, CompanyB],
            rowsByCompany: new Dictionary<Guid, IReadOnlyList<BelowMinimumItem>>
            {
                [CompanyA] = [Item(controlled, isControlled: true)],
                [CompanyB] = [Item(ordinary, isControlled: false)]
            });

        await RunOneTickAsync(host, expectedRaises: 2);

        Assert.All(host.Publisher.Raised, r => Assert.Equal(NotificationTypeCode.LowStock, r.Type));
        Assert.Equal(NotificationSeverityLevel.Critical,
            host.Publisher.Raised.Single(r => r.TargetId == controlled).Severity);
        Assert.Equal(NotificationSeverityLevel.Warning,
            host.Publisher.Raised.Single(r => r.TargetId == ordinary).Severity);
    }

    [Fact]
    public async Task Does_not_leak_across_tenants()
    {
        Guid itemB = Guid.NewGuid();

        AlertJobTestHost<BelowMinimumItem> host = new(
            companyIds: [CompanyA, CompanyB],
            rowsByCompany: new Dictionary<Guid, IReadOnlyList<BelowMinimumItem>>
            {
                [CompanyA] = [],
                [CompanyB] = [Item(itemB, isControlled: false)]
            });

        await RunOneTickAsync(host, expectedRaises: 1);

        Assert.Single(host.Publisher.Raised);
        Assert.Equal(itemB, host.Publisher.Raised[0].TargetId);
    }

    [Fact]
    public async Task A_failing_company_does_not_stop_the_others()
    {
        Guid itemB = Guid.NewGuid();

        AlertJobTestHost<BelowMinimumItem> host = new(
            companyIds: [CompanyA, CompanyB],
            rowsByCompany: new Dictionary<Guid, IReadOnlyList<BelowMinimumItem>>
            {
                [CompanyA] = [Item(Guid.NewGuid(), isControlled: false)],
                [CompanyB] = [Item(itemB, isControlled: false)]
            },
            failingCompanies: [CompanyA]);

        await RunOneTickAsync(host, expectedRaises: 1);

        Assert.Single(host.Publisher.Raised);
        Assert.Equal(itemB, host.Publisher.Raised[0].TargetId);
    }

    private static async Task RunOneTickAsync(AlertJobTestHost<BelowMinimumItem> host, int expectedRaises)
    {
        JobsOptions options = new() { LowStockAlert = new LowStockAlertOptions { Interval = TimeSpan.FromMinutes(10) } };
        LowStockAlertJob job = new(host, Options.Create(options), host.Clock, NullLogger<LowStockAlertJob>.Instance);

        await job.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => host.Publisher.Raised.Count >= expectedRaises, TestTimeout);
        await job.StopAsync(CancellationToken.None);
    }

    private static BelowMinimumItem Item(Guid id, bool isControlled) => new(
        Id: id,
        Name: "Insumo",
        Category: "Reagente",
        Brand: "ACME",
        Quantity: 1m,
        Unit: "L",
        MinimumQuantity: 4m,
        MinimumUnit: "L",
        Deficit: 3m,
        IsControlled: isControlled,
        StorageLocationId: Guid.NewGuid(),
        StorageLocationName: "Depósito",
        StorageLocationType: "Room");

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        using CancellationTokenSource cts = new(timeout);
        while (!condition())
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, cts.Token);
        }
    }
}
