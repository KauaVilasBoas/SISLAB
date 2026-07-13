using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SISLAB.Jobs.Configuration;
using SISLAB.Jobs.Jobs;
using SISLAB.Jobs.Tests.Jobs.TestSupport;
using SISLAB.Modules.Inventory.Application.StockRead;
using SISLAB.Modules.Notifications.Contracts;

namespace SISLAB.Jobs.Tests.Jobs;

/// <summary>
/// End-to-end tick tests for the validity/expiry alert job (#41). They drive the REAL job (and its base
/// <see cref="CompanyScanAlertJob"/>) through the hosted-service lifecycle over the in-memory
/// <see cref="AlertJobTestHost{TRow}"/>, asserting the produced notifications, per-cycle idempotency,
/// strict cross-tenant isolation and resilience to a single company's failure.
/// </summary>
public sealed class ExpiryAlertJobTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();

    [Fact]
    public async Task Raises_one_notification_per_at_risk_item_of_each_company()
    {
        Guid itemA = Guid.NewGuid();
        Guid itemB = Guid.NewGuid();

        AlertJobTestHost<ExpiringItem> host = new(
            companyIds: [CompanyA, CompanyB],
            rowsByCompany: new Dictionary<Guid, IReadOnlyList<ExpiringItem>>
            {
                [CompanyA] = [Item(itemA, ExpiryStatusView.Expired, -2)],
                [CompanyB] = [Item(itemB, ExpiryStatusView.ExpiringSoon, 5)]
            });

        await RunOneTickAsync(host, expectedRaises: 2);

        Assert.Equal(2, host.Publisher.Raised.Count);
        Assert.All(host.Publisher.Raised, r => Assert.Equal(NotificationTypeCode.Expiry, r.Type));

        RaiseNotificationRequest a = Single(host, itemA);
        RaiseNotificationRequest b = Single(host, itemB);
        Assert.Equal(NotificationSeverityLevel.Critical, a.Severity); // expired
        Assert.Equal(NotificationSeverityLevel.Warning, b.Severity);  // within 7 days
    }

    [Fact]
    public async Task Does_not_leak_across_tenants_company_A_never_alerts_with_company_B_data()
    {
        Guid itemA = Guid.NewGuid();
        Guid itemB = Guid.NewGuid();

        // Only company B has an at-risk item; company A has none. If the scan leaked B's data into A's
        // scope, we'd see two notifications for itemB (one per company). Correct isolation yields exactly one.
        AlertJobTestHost<ExpiringItem> host = new(
            companyIds: [CompanyA, CompanyB],
            rowsByCompany: new Dictionary<Guid, IReadOnlyList<ExpiringItem>>
            {
                [CompanyA] = [],
                [CompanyB] = [Item(itemB, ExpiryStatusView.ExpiringSoon, 3)]
            });

        await RunOneTickAsync(host, expectedRaises: 1);

        Assert.Single(host.Publisher.Raised);
        Assert.Equal(itemB, host.Publisher.Raised[0].TargetId);
        Assert.DoesNotContain(host.Publisher.Raised, r => r.TargetId == itemA);
    }

    [Fact]
    public async Task Is_idempotent_within_a_cycle_repeated_ticks_do_not_duplicate_alerts()
    {
        Guid itemA = Guid.NewGuid();

        AlertJobTestHost<ExpiringItem> host = new(
            companyIds: [CompanyA],
            rowsByCompany: new Dictionary<Guid, IReadOnlyList<ExpiringItem>>
            {
                [CompanyA] = [Item(itemA, ExpiryStatusView.ExpiringSoon, 5)]
            });

        // Fast interval so several ticks fire on the same (fixed) clock day; the day-bucketed dedupe key
        // must collapse them to a single active notification.
        ExpiryAlertJob job = NewJob(host, TimeSpan.FromMilliseconds(30));

        await job.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => host.BypassScopesOpened >= 3);
        await job.StopAsync(CancellationToken.None);

        Assert.Single(host.Publisher.Raised);
    }

    [Fact]
    public async Task A_failing_company_does_not_stop_the_scan_of_the_others()
    {
        Guid itemA = Guid.NewGuid();
        Guid itemB = Guid.NewGuid();

        AlertJobTestHost<ExpiringItem> host = new(
            companyIds: [CompanyA, CompanyB],
            rowsByCompany: new Dictionary<Guid, IReadOnlyList<ExpiringItem>>
            {
                [CompanyA] = [Item(itemA, ExpiryStatusView.ExpiringSoon, 5)],
                [CompanyB] = [Item(itemB, ExpiryStatusView.ExpiringSoon, 5)]
            },
            failingCompanies: [CompanyA]);

        await RunOneTickAsync(host, expectedRaises: 1);

        // Company A's scan threw; company B was still processed on the same tick.
        Assert.Single(host.Publisher.Raised);
        Assert.Equal(itemB, host.Publisher.Raised[0].TargetId);
    }

    [Fact]
    public async Task Opens_an_auditable_tenant_bypass_scope_per_tick()
    {
        AlertJobTestHost<ExpiringItem> host = new(
            companyIds: [CompanyA],
            rowsByCompany: new Dictionary<Guid, IReadOnlyList<ExpiringItem>>
            {
                [CompanyA] = [Item(Guid.NewGuid(), ExpiryStatusView.ExpiringSoon, 5)]
            });

        await RunOneTickAsync(host, expectedRaises: 1);

        Assert.True(host.BypassScopesOpened >= 1, "The job must open an auditable tenant-bypass scope.");
    }

    // ------------------------------------ helpers ------------------------------------

    private static async Task RunOneTickAsync(AlertJobTestHost<ExpiringItem> host, int expectedRaises)
    {
        // Large interval so only the immediate startup tick runs; we wait for its effect deterministically.
        ExpiryAlertJob job = NewJob(host, TimeSpan.FromMinutes(10));

        await job.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => host.Publisher.Raised.Count >= expectedRaises);
        await job.StopAsync(CancellationToken.None);
    }

    private static ExpiryAlertJob NewJob(AlertJobTestHost<ExpiringItem> host, TimeSpan interval)
    {
        JobsOptions options = new() { ExpiryAlert = new ExpiryAlertOptions { Interval = interval } };
        return new ExpiryAlertJob(host, Options.Create(options), host.Clock, NullLogger<ExpiryAlertJob>.Instance);
    }

    private static RaiseNotificationRequest Single(AlertJobTestHost<ExpiringItem> host, Guid itemId)
        => host.Publisher.Raised.Single(r => r.TargetId == itemId);

    private static ExpiringItem Item(Guid id, ExpiryStatusView status, int daysRemaining) => new(
        Id: id,
        Name: "Reagente X",
        Category: "Reagente",
        LotCode: "L-1",
        Quantity: 1m,
        Unit: "un",
        ExpiryYear: 2026,
        ExpiryMonth: 7,
        ExpiryStatus: status,
        DaysRemaining: daysRemaining,
        IsControlled: false,
        StorageLocationId: Guid.NewGuid(),
        StorageLocationName: "Armário",
        StorageLocationType: "Cabinet");

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
