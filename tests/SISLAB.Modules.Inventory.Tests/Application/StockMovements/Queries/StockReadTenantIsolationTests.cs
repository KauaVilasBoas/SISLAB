using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Inventory.Application.StockMovements.Queries;
using SISLAB.Modules.Inventory.Tests.Application.Configuration;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;
using Testcontainers.PostgreSql;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements.Queries;

/// <summary>
/// End-to-end tenant-isolation proof (card [E4] #34, part B) for the read-side StockRead queries. A real
/// PostgreSQL (spun up via Testcontainers) is seeded with two companies whose data overlaps in shape
/// (both have below-minimum items, expiring/expired items and consumption movements). The eight Dapper
/// handlers are then run — through the real <see cref="DbConnectionFactory"/> and the real handler code —
/// as company A, and each result is asserted to contain <b>only</b> company A's data and <b>never</b>
/// company B's. This complements the static guard (part A): the guard proves the filter is written, this
/// proves the filter actually isolates against a live engine, exercising the PG-specific SQL
/// (<c>make_date</c>, <c>INTERVAL</c>, <c>ILIKE</c>, <c>::date</c>, <c>date_trunc</c>, <c>COUNT(*) OVER()</c>).
/// </summary>
/// <remarks>
/// The tests are decorated with <see cref="DockerAvailableFactAttribute"/>, so on a leg without Docker they
/// are skipped rather than failing — keeping the suite green (section 10 DoD note about no guaranteed DB).
/// </remarks>
public sealed class StockReadTenantIsolationTests : IAsyncLifetime
{
    private static readonly Guid CompanyA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CompanyB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    // A fixed "today" so the derived expiry status is deterministic against the seeded validities.
    private static readonly DateTime Today = new(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly TodayDate = DateOnly.FromDateTime(Today);

    // --- Category catalogue (Configuration module, card [E12] #76). Each company owns its own categories;
    // the read-side view resolves the name from here, so A's items must reference A's categories only.
    private static readonly Guid A_SolventCategory = Guid.Parse("a0000005-0000-0000-0000-000000000001");
    private static readonly Guid A_ReagentCategory = Guid.Parse("a0000005-0000-0000-0000-000000000002");
    private static readonly Guid B_SolventCategory = Guid.Parse("b0000005-0000-0000-0000-000000000001");
    private static readonly Guid B_ReagentCategory = Guid.Parse("b0000005-0000-0000-0000-000000000002");

    // --- Company A seed ids (the ONLY ids any query run as A may return) -----------------------------
    private static readonly Guid A_FridgeLocation = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    private static readonly Guid A_ControlledLocation = Guid.Parse("a0000001-0000-0000-0000-000000000002");
    private static readonly Guid A_ItemOk = Guid.Parse("a0000002-0000-0000-0000-000000000001");        // valid, above minimum
    private static readonly Guid A_ItemExpiringSoon = Guid.Parse("a0000002-0000-0000-0000-000000000002"); // expiring within 30d
    private static readonly Guid A_ItemExpired = Guid.Parse("a0000002-0000-0000-0000-000000000003");   // already expired
    private static readonly Guid A_ItemBelowMinimum = Guid.Parse("a0000002-0000-0000-0000-000000000004"); // below minimum, no validity
    private static readonly Guid A_MovementConsumed = Guid.Parse("a0000003-0000-0000-0000-000000000001");
    private static readonly Guid A_Experiment = Guid.Parse("a0000004-0000-0000-0000-000000000001");

    // --- Company B seed ids (must NEVER surface when querying as A) -----------------------------------
    private static readonly Guid B_Location = Guid.Parse("b0000001-0000-0000-0000-000000000001");
    private static readonly Guid B_ItemExpired = Guid.Parse("b0000002-0000-0000-0000-000000000001");
    private static readonly Guid B_ItemBelowMinimum = Guid.Parse("b0000002-0000-0000-0000-000000000002");
    private static readonly Guid B_MovementConsumed = Guid.Parse("b0000003-0000-0000-0000-000000000001");

    private static readonly HashSet<Guid> CompanyBIds = new()
    {
        CompanyB, B_Location, B_ItemExpired, B_ItemBelowMinimum, B_MovementConsumed
    };

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private DbConnectionFactory _connectionFactory = null!;

    // Handlers under test, wired exactly like production: real DbConnectionFactory over the container, the
    // tenant context pinned to company A, and a fixed clock so the derived expiry status is deterministic.
    private ListStockItemsQueryHandler _listItems = null!;
    private GetLocationsSummaryQueryHandler _locationsSummary = null!;
    private GetExpirySummaryQueryHandler _expirySummary = null!;
    private ListExpiringItemsQueryHandler _expiringItems = null!;
    private ListItemsBelowMinimumQueryHandler _belowMinimum = null!;
    private GetBelowMinimumSummaryQueryHandler _belowMinimumSummary = null!;
    private GetConsumptionReportQueryHandler _consumptionReport = null!;
    private GetConsumptionSeriesQueryHandler _consumptionSeries = null!;

    public async Task InitializeAsync()
    {
        // When Docker is unavailable the DockerAvailableFact skips the tests, but IAsyncLifetime still runs;
        // starting the container would then throw. Reuse the attribute's probe and bail out early so the
        // lifecycle is a no-op (nothing is started, nothing to dispose).
        if (!DockerAvailableFactAttribute.IsDockerAvailable)
        {
            return;
        }

        await _container.StartAsync();

        // Mirror production wiring: the read-side needs the Dapper DateOnly/TimeOnly handlers registered
        // (AddSislabInfrastructure does this in the app) before any query binds a DateOnly parameter.
        DapperDateOnlyTypeHandlers.Register();

        _connectionFactory = new DbConnectionFactory(BuildConfiguration(_container.GetConnectionString()));

        ITenantContext tenantA = new StubTenantContext(CompanyA);
        IClock clock = new FixedClock(Today);

        // The two expiry-classifying read handlers resolve the warning window from the Configuration boundary
        // (card [E12] #76); the seed uses the 30-day window (this month is "expiring soon"), so the stub returns 30.
        var labConfiguration = new FakeLabConfiguration { WarningWindowDays = 30 };

        _listItems = new ListStockItemsQueryHandler(_connectionFactory, tenantA, clock, labConfiguration);
        _locationsSummary = new GetLocationsSummaryQueryHandler(_connectionFactory, tenantA, clock);
        _expirySummary = new GetExpirySummaryQueryHandler(_connectionFactory, tenantA, clock, labConfiguration);
        _expiringItems = new ListExpiringItemsQueryHandler(_connectionFactory, tenantA, clock);
        _belowMinimum = new ListItemsBelowMinimumQueryHandler(_connectionFactory, tenantA);
        _belowMinimumSummary = new GetBelowMinimumSummaryQueryHandler(_connectionFactory, tenantA);
        _consumptionReport = new GetConsumptionReportQueryHandler(_connectionFactory, tenantA);
        _consumptionSeries = new GetConsumptionSeriesQueryHandler(_connectionFactory, tenantA);

        await CreateSchemaAsync();
        await SeedAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    // -------------------------------------------------------------------------------------------------
    // The eight queries. Each runs as company A and asserts (a) it sees A's seeded data and (b) it never
    // leaks any company B id — the tenant filter holds against a live PostgreSQL.
    // -------------------------------------------------------------------------------------------------

    [DockerAvailableFact]
    public async Task List_stock_items_returns_only_company_a_items()
    {
        var result = await _listItems.HandleAsync(new ListStockItemsQuery { PageSize = 200 });

        Assert.Equal(4, result.TotalCount); // A's four items, none of B's
        Assert.All(result.Items, item => AssertNotCompanyB(item.Id, item.StorageLocationId));
        Assert.Contains(result.Items, item => item.Id == A_ItemBelowMinimum && item.IsBelowMinimum);
        Assert.Contains(result.Items, item => item.Id == A_ItemExpired && item.ExpiryStatus == ExpiryStatusView.Expired);
        Assert.DoesNotContain(result.Items, item => item.Id == B_ItemExpired);
    }

    [DockerAvailableFact]
    public async Task List_stock_items_search_never_matches_company_b()
    {
        // "shared-name" is seeded on items of BOTH companies; the ILIKE search must still only see A's.
        var result = await _listItems.HandleAsync(new ListStockItemsQuery { Search = "shared-name", PageSize = 200 });

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => AssertNotCompanyB(item.Id, item.StorageLocationId));
    }

    [DockerAvailableFact]
    public async Task Locations_summary_returns_only_company_a_locations()
    {
        IReadOnlyList<LocationSummaryItem> result = await _locationsSummary.HandleAsync(new GetLocationsSummaryQuery());

        Assert.Equal(2, result.Count); // A's fridge + controlled box, not B's location
        Assert.All(result, location => AssertNotCompanyB(location.Id));
        Assert.DoesNotContain(result, location => location.Id == B_Location);

        LocationSummaryItem controlled = Assert.Single(result, location => location.Id == A_ControlledLocation);
        Assert.True(controlled.IsCritical);

        // The expired-item count is A's alone (B's expired item must not bleed into A's location counts).
        LocationSummaryItem fridge = Assert.Single(result, location => location.Id == A_FridgeLocation);
        Assert.Equal(1, fridge.ExpiredItemCount);
    }

    [DockerAvailableFact]
    public async Task Expiry_summary_counts_only_company_a_items()
    {
        ExpirySummary result = await _expirySummary.HandleAsync(new GetExpirySummaryQuery());

        // A has exactly one Ok, one ExpiringSoon and one Expired item carrying a validity (the below-minimum
        // item has no validity and is excluded). B's expired item must not be counted.
        Assert.Equal(1, result.Expired);
        Assert.Equal(1, result.ExpiringSoon);
        Assert.Equal(1, result.Ok);
        Assert.Equal(3, result.Total);
    }

    [DockerAvailableFact]
    public async Task Expiring_items_returns_only_company_a_at_risk_items()
    {
        var result = await _expiringItems.HandleAsync(new ListExpiringItemsQuery { PageSize = 200 });

        Assert.All(result.Items, item => AssertNotCompanyB(item.Id, item.StorageLocationId));
        Assert.Contains(result.Items, item => item.Id == A_ItemExpiringSoon);
        Assert.Contains(result.Items, item => item.Id == A_ItemExpired);
        Assert.DoesNotContain(result.Items, item => item.Id == B_ItemExpired);
    }

    [DockerAvailableFact]
    public async Task Items_below_minimum_returns_only_company_a_items()
    {
        var result = await _belowMinimum.HandleAsync(new ListItemsBelowMinimumQuery { PageSize = 200 });

        Assert.All(result.Items, item => AssertNotCompanyB(item.Id, item.StorageLocationId));
        Assert.Contains(result.Items, item => item.Id == A_ItemBelowMinimum);
        Assert.DoesNotContain(result.Items, item => item.Id == B_ItemBelowMinimum);
    }

    [DockerAvailableFact]
    public async Task Below_minimum_summary_counts_only_company_a_items()
    {
        BelowMinimumSummary result = await _belowMinimumSummary.HandleAsync(new GetBelowMinimumSummaryQuery());

        // A has exactly one below-minimum item; B's below-minimum item must not be counted.
        Assert.Equal(1, result.BelowMinimumCount);
    }

    [DockerAvailableFact]
    public async Task Consumption_report_returns_only_company_a_movements()
    {
        var query = new GetConsumptionReportQuery
        {
            From = new DateOnly(2026, 6, 1),
            To = new DateOnly(2026, 7, 31),
            PageSize = 200
        };

        ConsumptionReport result = await _consumptionReport.HandleAsync(query);

        // Only A's consumption (its single consumed movement, 5 units) — B's movement is a different unit
        // and quantity, so a leak would change both the row set and the grand totals.
        ConsumptionReportItem row = Assert.Single(result.Items.Items);
        Assert.Equal(A_ItemBelowMinimum, row.StockItemId);
        Assert.Equal(5m, row.TotalConsumed);

        ConsumptionTotal total = Assert.Single(result.Totals);
        Assert.Equal(5m, total.TotalConsumed);
        Assert.DoesNotContain(result.Items.Items, item => item.StockItemId == B_ItemBelowMinimum);
    }

    [DockerAvailableFact]
    public async Task Consumption_series_totals_only_company_a_movements()
    {
        var query = new GetConsumptionSeriesQuery
        {
            From = new DateOnly(2026, 6, 1),
            To = new DateOnly(2026, 7, 31)
        };

        ConsumptionSeries result = await _consumptionSeries.HandleAsync(query);

        // A's single consumed movement (5 units) is the whole current-period total; B's movement must not add in.
        ConsumptionPeriodTotal total = Assert.Single(result.Totals);
        Assert.Equal(5m, total.CurrentTotal);
        Assert.All(result.Points, point => Assert.Equal(5m, point.TotalConsumed));
    }

    // -------------------------------------------------------------------------------------------------
    // Schema + seed. The DDL mirrors the Inventory migrations (InitialInventorySchema + AddInventoryReadModels):
    // stock_items, storage_locations, stock_movements and the stock_view, all in the inventory schema.
    // -------------------------------------------------------------------------------------------------

    private async Task CreateSchemaAsync()
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            """
            CREATE SCHEMA IF NOT EXISTS inventory;
            CREATE SCHEMA IF NOT EXISTS configuration;

            -- The per-tenant category catalogue owned by the Configuration module (card [E12] #76). The
            -- read-side stock_view resolves the human-readable category name from here through a tenant-safe
            -- join; Inventory only stores category_id by value.
            CREATE TABLE configuration.item_categories (
                id          uuid PRIMARY KEY,
                company_id  uuid NOT NULL,
                name        varchar(120) NOT NULL,
                is_controlled boolean NOT NULL
            );

            CREATE TABLE inventory.storage_locations (
                id          uuid PRIMARY KEY,
                company_id  uuid NOT NULL,
                name        varchar(150) NOT NULL,
                type        varchar(30)  NOT NULL,
                description varchar(500),
                temp_min    numeric(6,2),
                temp_max    numeric(6,2),
                is_active   boolean NOT NULL
            );

            CREATE TABLE inventory.stock_items (
                id                      uuid PRIMARY KEY,
                company_id              uuid NOT NULL,
                name                    varchar(200) NOT NULL,
                category_id             uuid NOT NULL,
                brand                   varchar(120),
                container_state         varchar(20)  NOT NULL,
                application             varchar(500),
                is_controlled           boolean NOT NULL,
                storage_location_id     uuid NOT NULL,
                lot_code                varchar(64),
                expiry_year             integer,
                expiry_month            integer,
                minimum_quantity_unit   varchar(20)  NOT NULL,
                minimum_quantity_amount numeric(18,4) NOT NULL,
                quantity_unit           varchar(20)  NOT NULL,
                quantity_amount         numeric(18,4) NOT NULL
            );

            CREATE TABLE inventory.stock_movements (
                id              uuid PRIMARY KEY,
                company_id      uuid NOT NULL,
                stock_item_id   uuid NOT NULL,
                movement_type   varchar(20) NOT NULL,
                quantity_amount numeric(18,4) NOT NULL,
                quantity_unit   varchar(20) NOT NULL,
                occurred_on     date,
                experiment_id   uuid,
                partner_id      uuid,
                performed_by    uuid,
                created_at_utc  timestamptz NOT NULL
            );

            CREATE VIEW inventory.stock_view AS
            SELECT
                si.id                       AS id,
                si.company_id               AS company_id,
                si.name                     AS name,
                si.category_id              AS category_id,
                ic.name                     AS category,
                si.brand                    AS brand,
                si.container_state          AS container_state,
                si.application              AS application,
                si.is_controlled            AS is_controlled,
                si.quantity_amount          AS quantity_amount,
                si.quantity_unit            AS quantity_unit,
                si.minimum_quantity_amount  AS minimum_quantity_amount,
                si.minimum_quantity_unit    AS minimum_quantity_unit,
                (si.quantity_amount < si.minimum_quantity_amount) AS is_below_minimum,
                si.lot_code                 AS lot_code,
                si.expiry_year              AS expiry_year,
                si.expiry_month             AS expiry_month,
                si.storage_location_id      AS storage_location_id,
                sl.name                     AS storage_location_name,
                sl.type                     AS storage_location_type,
                sl.is_active                AS storage_location_is_active
            FROM inventory.stock_items AS si
            LEFT JOIN inventory.storage_locations AS sl
                ON sl.id = si.storage_location_id
               AND sl.company_id = si.company_id
            LEFT JOIN configuration.item_categories AS ic
                ON ic.id = si.category_id
               AND ic.company_id = si.company_id;
            """);
    }

    private async Task SeedAsync()
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();

        // --- Category catalogue ----------------------------------------------------------------------
        await connection.ExecuteAsync(
            """
            INSERT INTO configuration.item_categories (id, company_id, name, is_controlled) VALUES
                (@A_Solvent, @CompanyA, 'Solvent', false),
                (@A_Reagent, @CompanyA, 'Reagent', true),
                (@B_Solvent, @CompanyB, 'Solvent', false),
                (@B_Reagent, @CompanyB, 'Reagent', false);
            """,
            new
            {
                A_Solvent = A_SolventCategory,
                A_Reagent = A_ReagentCategory,
                B_Solvent = B_SolventCategory,
                B_Reagent = B_ReagentCategory,
                CompanyA,
                CompanyB
            });

        // --- Storage locations -----------------------------------------------------------------------
        await connection.ExecuteAsync(
            """
            INSERT INTO inventory.storage_locations (id, company_id, name, type, is_active) VALUES
                (@A_Fridge, @CompanyA, 'Geladeira A', 'Refrigerator', true),
                (@A_Controlled, @CompanyA, 'Armario Controlado A', 'Controlled', true),
                (@B_Location, @CompanyB, 'Geladeira B', 'Refrigerator', true);
            """,
            new { A_Fridge = A_FridgeLocation, A_Controlled = A_ControlledLocation, B_Location, CompanyA, CompanyB });

        // --- Stock items -----------------------------------------------------------------------------
        // Company A: Ok, ExpiringSoon and Expired items in the fridge (all with validity, above minimum),
        // plus a below-minimum item with no validity in the controlled box. "shared-name" appears on A and B.
        int expiringYear = TodayDate.Year;
        int expiringMonth = TodayDate.Month; // last day of this month is within the 30-day window from today.
        var expired = TodayDate.AddMonths(-2);

        await connection.ExecuteAsync(
            """
            INSERT INTO inventory.stock_items
                (id, company_id, name, category_id, brand, container_state, is_controlled, storage_location_id,
                 lot_code, expiry_year, expiry_month, minimum_quantity_unit, minimum_quantity_amount,
                 quantity_unit, quantity_amount)
            VALUES
                (@A_ItemOk, @CompanyA, 'Etanol shared-name A', @A_Solvent, 'AcmeA', 'Sealed', false, @A_Fridge,
                 'LA-OK', 2030, 1, 'mL', 100, 'mL', 500),
                (@A_ItemExpiringSoon, @CompanyA, 'Metanol A', @A_Solvent, 'AcmeA', 'Sealed', false, @A_Fridge,
                 'LA-SOON', @ExpYear, @ExpMonth, 'mL', 100, 'mL', 500),
                (@A_ItemExpired, @CompanyA, 'Acetona A', @A_Solvent, 'AcmeA', 'Sealed', false, @A_Fridge,
                 'LA-EXP', @ExpiredYear, @ExpiredMonth, 'mL', 100, 'mL', 500),
                (@A_ItemBelowMinimum, @CompanyA, 'Reagente controlado shared-name A', @A_Reagent, 'AcmeA', 'Open',
                 true, @A_Controlled, 'LA-LOW', NULL, NULL, 'g', 100, 'g', 10),
                (@B_ItemExpired, @CompanyB, 'Acetona shared-name B', @B_Solvent, 'AcmeB', 'Sealed', false, @B_Location,
                 'LB-EXP', @ExpiredYear, @ExpiredMonth, 'mL', 100, 'mL', 500),
                (@B_ItemBelowMinimum, @CompanyB, 'Reagente shared-name B', @B_Reagent, 'AcmeB', 'Open', false,
                 @B_Location, 'LB-LOW', NULL, NULL, 'g', 100, 'g', 5);
            """,
            new
            {
                A_ItemOk,
                A_ItemExpiringSoon,
                A_ItemExpired,
                A_ItemBelowMinimum,
                B_ItemExpired,
                B_ItemBelowMinimum,
                A_Solvent = A_SolventCategory,
                A_Reagent = A_ReagentCategory,
                B_Solvent = B_SolventCategory,
                B_Reagent = B_ReagentCategory,
                A_Fridge = A_FridgeLocation,
                A_Controlled = A_ControlledLocation,
                B_Location,
                CompanyA,
                CompanyB,
                ExpYear = expiringYear,
                ExpMonth = expiringMonth,
                ExpiredYear = expired.Year,
                ExpiredMonth = expired.Month
            });

        // --- Movements (consumption ledger) ----------------------------------------------------------
        // A consumed 5 g of its controlled reagent inside the report window; B consumed 99 mL of its item.
        await connection.ExecuteAsync(
            """
            INSERT INTO inventory.stock_movements
                (id, company_id, stock_item_id, movement_type, quantity_amount, quantity_unit,
                 occurred_on, experiment_id, partner_id, performed_by, created_at_utc)
            VALUES
                (@A_Movement, @CompanyA, @A_ItemBelowMinimum, 'Consumed', 5, 'g',
                 @OccurredOn, @A_Experiment, NULL, NULL, @Now),
                (@B_Movement, @CompanyB, @B_ItemBelowMinimum, 'Consumed', 99, 'mL',
                 @OccurredOn, NULL, NULL, NULL, @Now);
            """,
            new
            {
                A_Movement = A_MovementConsumed,
                B_Movement = B_MovementConsumed,
                A_ItemBelowMinimum,
                B_ItemBelowMinimum,
                A_Experiment,
                CompanyA,
                CompanyB,
                OccurredOn = new DateOnly(2026, 7, 1),
                Now = Today
            });
    }

    /// <summary>Fails the test if any of the given ids belongs to company B — the tenant-leak assertion.</summary>
    private static void AssertNotCompanyB(params Guid[] ids)
    {
        foreach (Guid id in ids)
        {
            Assert.DoesNotContain(id, CompanyBIds);
        }
    }

    private static IConfiguration BuildConfiguration(string connectionString)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SislabDb"] = connectionString
            })
            .Build();
}
