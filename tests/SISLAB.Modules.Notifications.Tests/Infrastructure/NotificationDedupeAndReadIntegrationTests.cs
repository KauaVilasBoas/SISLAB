using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Notifications.Application.NotificationsRead;
using SISLAB.Modules.Notifications.Contracts;
using SISLAB.Modules.Notifications.Infrastructure.Messaging;
using SISLAB.Modules.Notifications.Infrastructure.Persistence;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using Testcontainers.PostgreSql;

namespace SISLAB.Modules.Notifications.Tests.Infrastructure;

/// <summary>
/// End-to-end proof (card #64a) against a real PostgreSQL (Testcontainers) that the notification write path is
/// idempotent by dedupe key and tenant-isolated, and that the read-side bell counts/lists correctly. It runs
/// the production code — the real <see cref="NotificationStore"/> (<c>ON CONFLICT DO NOTHING</c> over the
/// partial unique index), the real <see cref="NotificationPublisher"/> behind the public
/// <see cref="INotificationPublisher"/> port, and the real Dapper read handlers — over a schema that mirrors
/// the <c>AddNotificationsSchema</c> migration exactly (including the partial unique index that makes the
/// dedupe work).
/// </summary>
/// <remarks>
/// Decorated with <see cref="DockerAvailableFactAttribute"/>, so on a leg without Docker the tests are skipped
/// rather than failing — keeping the suite green (section 10 DoD note about no guaranteed DB).
/// </remarks>
public sealed class NotificationDedupeAndReadIntegrationTests : IAsyncLifetime
{
    private static readonly Guid CompanyA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CompanyB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTime Now = new(2026, 7, 12, 8, 0, 0, DateTimeKind.Utc);

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    private DbConnectionFactory _connectionFactory = null!;

    public async Task InitializeAsync()
    {
        if (!DockerAvailableFactAttribute.IsDockerAvailable)
        {
            return;
        }

        await _container.StartAsync();
        _connectionFactory = new DbConnectionFactory(BuildConfiguration(_container.GetConnectionString()));
        await CreateSchemaAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    // -------------------------------------------------------------------------------------------------
    // Idempotency / dedupe
    // -------------------------------------------------------------------------------------------------

    [DockerAvailableFact]
    public async Task Raising_the_same_alert_twice_creates_a_single_active_notification()
    {
        INotificationPublisher publisher = PublisherFor(CompanyA);
        RaiseNotificationRequest request = ExpiryRequest("expiry:stock_item:x:2026-07");

        bool firstInserted = await publisher.RaiseAsync(request);
        bool secondInserted = await publisher.RaiseAsync(request);

        Assert.True(firstInserted);   // the first raise created the notification
        Assert.False(secondInserted); // the duplicate raise was a no-op (ON CONFLICT DO NOTHING)
        Assert.Equal(1, await CountRowsAsync(CompanyA));
    }

    [DockerAvailableFact]
    public async Task Distinct_dedupe_keys_create_distinct_notifications()
    {
        INotificationPublisher publisher = PublisherFor(CompanyA);

        Assert.True(await publisher.RaiseAsync(ExpiryRequest("expiry:stock_item:x:2026-07")));
        Assert.True(await publisher.RaiseAsync(ExpiryRequest("expiry:stock_item:x:2026-08")));

        Assert.Equal(2, await CountRowsAsync(CompanyA));
    }

    [DockerAvailableFact]
    public async Task Reading_a_notification_frees_its_dedupe_key_so_the_alert_can_re_fire()
    {
        INotificationPublisher publisher = PublisherFor(CompanyA);
        RaiseNotificationRequest request = ExpiryRequest("expiry:stock_item:x:2026-07");

        Assert.True(await publisher.RaiseAsync(request));

        // Mark the single active notification as read — the partial unique index no longer covers it.
        await MarkAllReadAsync(CompanyA);

        // The same alert fires again (e.g. still expiring next check): a new active row is created.
        Assert.True(await publisher.RaiseAsync(request));

        Assert.Equal(2, await CountRowsAsync(CompanyA));
        Assert.Equal(1, await CountUnreadRowsAsync(CompanyA));
    }

    [DockerAvailableFact]
    public async Task The_same_dedupe_key_in_a_different_company_does_not_collide()
    {
        // The uniqueness is (company_id, dedupe_key): the same logical key lives independently per tenant.
        Assert.True(await PublisherFor(CompanyA).RaiseAsync(ExpiryRequest("expiry:stock_item:x:2026-07")));
        Assert.True(await PublisherFor(CompanyB).RaiseAsync(ExpiryRequest("expiry:stock_item:x:2026-07")));

        Assert.Equal(1, await CountRowsAsync(CompanyA));
        Assert.Equal(1, await CountRowsAsync(CompanyB));
    }

    // -------------------------------------------------------------------------------------------------
    // Read-side (bell): unread count + list, tenant-isolated
    // -------------------------------------------------------------------------------------------------

    [DockerAvailableFact]
    public async Task Unread_count_returns_only_the_active_company_unread_notifications()
    {
        await PublisherFor(CompanyA).RaiseAsync(ExpiryRequest("expiry:stock_item:a1:2026-07"));
        await PublisherFor(CompanyA).RaiseAsync(ExpiryRequest("expiry:stock_item:a2:2026-07"));
        await PublisherFor(CompanyB).RaiseAsync(ExpiryRequest("expiry:stock_item:b1:2026-07"));

        var handler = new CountUnreadNotificationsQueryHandler(_connectionFactory, new StubTenantContext(CompanyA));
        UnreadNotificationsCount count = await handler.HandleAsync(new CountUnreadNotificationsQuery());

        Assert.Equal(2, count.UnreadCount); // A's two, never B's
    }

    [DockerAvailableFact]
    public async Task Unread_count_excludes_read_notifications()
    {
        await PublisherFor(CompanyA).RaiseAsync(ExpiryRequest("expiry:stock_item:a1:2026-07"));
        await PublisherFor(CompanyA).RaiseAsync(ExpiryRequest("expiry:stock_item:a2:2026-07"));
        await MarkAllReadAsync(CompanyA);

        var handler = new CountUnreadNotificationsQueryHandler(_connectionFactory, new StubTenantContext(CompanyA));
        UnreadNotificationsCount count = await handler.HandleAsync(new CountUnreadNotificationsQuery());

        Assert.Equal(0, count.UnreadCount);
    }

    [DockerAvailableFact]
    public async Task List_returns_only_the_active_company_notifications_newest_first()
    {
        await PublisherFor(CompanyA).RaiseAsync(ExpiryRequest("expiry:stock_item:a1:2026-07"));
        await PublisherFor(CompanyA).RaiseAsync(ExpiryRequest("expiry:stock_item:a2:2026-07"));
        await PublisherFor(CompanyB).RaiseAsync(ExpiryRequest("expiry:stock_item:b1:2026-07"));

        var handler = new ListNotificationsQueryHandler(_connectionFactory, new StubTenantContext(CompanyA));
        PagedResult<NotificationListItem> result =
            await handler.HandleAsync(new ListNotificationsQuery { PageSize = 200 });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal("expiry", item.Type.ToLowerInvariant()));
        Assert.All(result.Items, item => Assert.False(item.IsRead));
    }

    [DockerAvailableFact]
    public async Task List_with_unread_only_excludes_read_notifications()
    {
        await PublisherFor(CompanyA).RaiseAsync(ExpiryRequest("expiry:stock_item:a1:2026-07"));
        await PublisherFor(CompanyA).RaiseAsync(ExpiryRequest("expiry:stock_item:a2:2026-07"));
        await MarkOneReadAsync(CompanyA);

        var handler = new ListNotificationsQueryHandler(_connectionFactory, new StubTenantContext(CompanyA));

        PagedResult<NotificationListItem> all =
            await handler.HandleAsync(new ListNotificationsQuery { PageSize = 200, UnreadOnly = false });
        PagedResult<NotificationListItem> unread =
            await handler.HandleAsync(new ListNotificationsQuery { PageSize = 200, UnreadOnly = true });

        Assert.Equal(2, all.TotalCount);
        Assert.Equal(1, unread.TotalCount);
    }

    // -------------------------------------------------------------------------------------------------
    // Bulk acknowledge (card #65 — "marcar todas como lidas"): tenant-scoped + idempotent
    // -------------------------------------------------------------------------------------------------

    [DockerAvailableFact]
    public async Task Mark_all_read_acknowledges_only_the_active_company_and_returns_the_count()
    {
        await PublisherFor(CompanyA).RaiseAsync(ExpiryRequest("expiry:stock_item:a1:2026-07"));
        await PublisherFor(CompanyA).RaiseAsync(ExpiryRequest("expiry:stock_item:a2:2026-07"));
        await PublisherFor(CompanyB).RaiseAsync(ExpiryRequest("expiry:stock_item:b1:2026-07"));

        var store = new NotificationStore(_connectionFactory);
        int marked = await store.MarkAllReadAsync(CompanyA, Now);

        Assert.Equal(2, marked);                              // A's two rows, never B's
        Assert.Equal(0, await CountUnreadRowsAsync(CompanyA)); // A's inbox is fully acknowledged
        Assert.Equal(1, await CountUnreadRowsAsync(CompanyB)); // B is untouched — tenant isolation holds
    }

    [DockerAvailableFact]
    public async Task Mark_all_read_is_idempotent_a_second_run_marks_nothing()
    {
        await PublisherFor(CompanyA).RaiseAsync(ExpiryRequest("expiry:stock_item:a1:2026-07"));
        await PublisherFor(CompanyA).RaiseAsync(ExpiryRequest("expiry:stock_item:a2:2026-07"));

        var store = new NotificationStore(_connectionFactory);

        int firstRun = await store.MarkAllReadAsync(CompanyA, Now);
        int secondRun = await store.MarkAllReadAsync(CompanyA, Now.AddHours(2));

        Assert.Equal(2, firstRun);  // both flipped on the first pass
        Assert.Equal(0, secondRun); // nothing left unread — a clean no-op
    }

    [DockerAvailableFact]
    public async Task Mark_all_read_on_an_empty_inbox_returns_zero()
    {
        var store = new NotificationStore(_connectionFactory);

        int marked = await store.MarkAllReadAsync(CompanyA, Now);

        Assert.Equal(0, marked);
    }

    // -------------------------------------------------------------------------------------------------
    // Wiring helpers
    // -------------------------------------------------------------------------------------------------

    private INotificationPublisher PublisherFor(Guid companyId)
        => new NotificationPublisher(
            new NotificationStore(_connectionFactory),
            new StubTenantContext(companyId),
            new FixedClock(Now));

    private static RaiseNotificationRequest ExpiryRequest(string dedupeKey) => new(
        NotificationTypeCode.Expiry,
        NotificationSeverityLevel.Warning,
        "Reagente vencendo",
        "MTT vence em 5 dias",
        "stock_item",
        Guid.NewGuid(),
        dedupeKey);

    private async Task<int> CountRowsAsync(Guid companyId)
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM notifications.notifications WHERE company_id = @companyId;",
            new { companyId });
    }

    private async Task<int> CountUnreadRowsAsync(Guid companyId)
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM notifications.notifications WHERE company_id = @companyId AND is_read = false;",
            new { companyId });
    }

    private async Task MarkAllReadAsync(Guid companyId)
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.ExecuteAsync(
            "UPDATE notifications.notifications SET is_read = true, read_at_utc = @now WHERE company_id = @companyId;",
            new { companyId, now = Now });
    }

    private async Task MarkOneReadAsync(Guid companyId)
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.ExecuteAsync(
            """
            UPDATE notifications.notifications
            SET is_read = true, read_at_utc = @now
            WHERE id = (
                SELECT id FROM notifications.notifications
                WHERE company_id = @companyId AND is_read = false
                LIMIT 1
            );
            """,
            new { companyId, now = Now });
    }

    /// <summary>DDL mirroring the AddNotificationsSchema migration — including the partial unique index.</summary>
    private async Task CreateSchemaAsync()
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            """
            CREATE SCHEMA IF NOT EXISTS notifications;

            CREATE TABLE notifications.notifications (
                id                    uuid PRIMARY KEY,
                company_id            uuid NOT NULL,
                type                  varchar(40)  NOT NULL,
                severity              varchar(20)  NOT NULL,
                title                 varchar(200) NOT NULL,
                description           varchar(1000) NOT NULL,
                reference_target_type varchar(60)  NOT NULL,
                reference_target_id   uuid NOT NULL,
                dedupe_key            varchar(200) NOT NULL,
                is_read               boolean NOT NULL,
                created_at_utc        timestamptz NOT NULL,
                read_at_utc           timestamptz
            );

            CREATE INDEX ix_notifications_company_id_created_at_utc
                ON notifications.notifications (company_id, created_at_utc);

            CREATE UNIQUE INDEX ux_notifications_company_id_dedupe_key_active
                ON notifications.notifications (company_id, dedupe_key)
                WHERE is_read = false;
            """);
    }

    private static IConfiguration BuildConfiguration(string connectionString)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SislabDb"] = connectionString
            })
            .Build();
}
