using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Introduces the per-batch stock model (card [E4] #109): the balance, lot, expiry and cost move from a
    /// single row on <c>stock_items</c> to an owned collection of <c>stock_batches</c> (one row per receipt,
    /// each with its own remaining balance, validity and unit cost), so consumption can be drawn FEFO and the
    /// cost report can value each draw at the real per-batch price. It also extends the projected
    /// <c>stock_movements</c> read model with <c>stock_batch_id</c> and <c>unit_cost_brl</c> (the batch and
    /// cost each movement was charged against), and rewrites the current-state <c>stock_view</c> so its balance
    /// and current lot/validity are derived from the batches instead of the dropped item columns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ordering.</b> The view depends on the item columns being dropped (<c>quantity_amount</c>,
    /// <c>lot_code</c>, <c>expiry_*</c>) and on <c>quantity_unit</c> being renamed to <c>unit</c>, so it is
    /// dropped first and recreated last, over the new schema. The EF-scaffolded column/table changes run in
    /// between.
    /// </para>
    /// <para>
    /// <b>No data backfill.</b> The database is provisioned from migrations with no seeded stock, so the swap is
    /// a plain drop+create (the user drops and recreates the local database). A tenant with live data would need
    /// a data migration turning each item's single balance into an opening batch.
    /// </para>
    /// </remarks>
    /// <inheritdoc />
    public partial class AddStockBatchesAndMovementCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The current-state view reads the item columns dropped/renamed below; drop it before they change.
            migrationBuilder.Sql("DROP VIEW IF EXISTS inventory.stock_view;");

            migrationBuilder.DropColumn(
                name: "expiry_month",
                schema: "inventory",
                table: "stock_items");

            migrationBuilder.DropColumn(
                name: "expiry_year",
                schema: "inventory",
                table: "stock_items");

            migrationBuilder.DropColumn(
                name: "lot_code",
                schema: "inventory",
                table: "stock_items");

            migrationBuilder.DropColumn(
                name: "quantity_amount",
                schema: "inventory",
                table: "stock_items");

            migrationBuilder.RenameColumn(
                name: "quantity_unit",
                schema: "inventory",
                table: "stock_items",
                newName: "unit");

            migrationBuilder.CreateTable(
                name: "stock_batches",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    initial_quantity_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    initial_quantity_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    remaining_quantity_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    remaining_quantity_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    lot_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    expiry_year = table.Column<int>(type: "integer", nullable: true),
                    expiry_month = table.Column<int>(type: "integer", nullable: true),
                    unit_cost_brl = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    received_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    supplier_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stock_item_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_batches", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_batches_stock_items_stock_item_id",
                        column: x => x.stock_item_id,
                        principalSchema: "inventory",
                        principalTable: "stock_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_batches_stock_item_id",
                schema: "inventory",
                table: "stock_batches",
                column: "stock_item_id");

            // --- Read model (stock_movements): batch + cost columns (card [E4] #109) -----------------
            // Not an EF entity (written by the projection's raw SQL / read via Dapper), so the columns are
            // added as raw DDL. stock_batch_id points at the batch the movement was charged against
            // (ON DELETE SET NULL keeps the ledger row if the batch is later removed); unit_cost_brl is the
            // unit cost captured at movement time. Both are nullable: entries/transfers or unpriced draws
            // carry none.
            migrationBuilder.Sql(
                """
                ALTER TABLE inventory.stock_movements
                    ADD COLUMN stock_batch_id uuid NULL,
                    ADD COLUMN unit_cost_brl  numeric(12,4) NULL;

                ALTER TABLE inventory.stock_movements
                    ADD CONSTRAINT fk_stock_movements_stock_batch
                    FOREIGN KEY (stock_batch_id)
                    REFERENCES inventory.stock_batches (id)
                    ON DELETE SET NULL;
                """);

            // --- Current-state view rebuilt over the batch ledger ------------------------------------
            // The balance is the SUM of the item's remaining batch balances; the current lot/validity are the
            // FEFO batch's (earliest expiry, nulls last, then earliest receipt) among batches that still have
            // balance — matching what the aggregate would draw next and what the item listing shows. The column
            // names/shape are unchanged (quantity_amount, quantity_unit, lot_code, expiry_year, expiry_month,
            // is_below_minimum), so every downstream read query keeps working without a change. The category
            // name is still resolved through the tenant-safe LEFT JOIN onto the Configuration catalogue.
            migrationBuilder.Sql(
                """
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
                    COALESCE(bal.quantity_amount, 0) AS quantity_amount,
                    si.unit                     AS quantity_unit,
                    si.minimum_quantity_amount  AS minimum_quantity_amount,
                    si.minimum_quantity_unit    AS minimum_quantity_unit,
                    (COALESCE(bal.quantity_amount, 0) < si.minimum_quantity_amount) AS is_below_minimum,
                    fefo.lot_code               AS lot_code,
                    fefo.expiry_year            AS expiry_year,
                    fefo.expiry_month           AS expiry_month,
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
                   AND ic.company_id = si.company_id
                LEFT JOIN LATERAL (
                    SELECT SUM(sb.remaining_quantity_amount) AS quantity_amount
                    FROM inventory.stock_batches AS sb
                    WHERE sb.stock_item_id = si.id
                ) AS bal ON true
                LEFT JOIN LATERAL (
                    SELECT sb.lot_code, sb.expiry_year, sb.expiry_month
                    FROM inventory.stock_batches AS sb
                    WHERE sb.stock_item_id = si.id
                      AND sb.remaining_quantity_amount > 0
                    ORDER BY
                        (sb.expiry_year IS NULL) ASC,
                        sb.expiry_year ASC,
                        sb.expiry_month ASC,
                        sb.received_at_utc ASC
                    LIMIT 1
                ) AS fefo ON true;
                """);

            // Report access path for the cost queries (card #109): consumption cost over time by company.
            migrationBuilder.Sql(
                """
                CREATE INDEX ix_stock_movements_company_cost
                    ON inventory.stock_movements (company_id, movement_type)
                    WHERE unit_cost_brl IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the view (depends on the new schema) and the read-model additions before the write-side
            // schema is rolled back. The FK to stock_batches must go before the table is dropped, and the
            // partial cost index and the two columns are removed too.
            migrationBuilder.Sql("DROP VIEW IF EXISTS inventory.stock_view;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS inventory.ix_stock_movements_company_cost;");
            migrationBuilder.Sql(
                """
                ALTER TABLE inventory.stock_movements
                    DROP CONSTRAINT IF EXISTS fk_stock_movements_stock_batch;

                ALTER TABLE inventory.stock_movements
                    DROP COLUMN IF EXISTS stock_batch_id,
                    DROP COLUMN IF EXISTS unit_cost_brl;
                """);

            migrationBuilder.DropTable(
                name: "stock_batches",
                schema: "inventory");

            migrationBuilder.RenameColumn(
                name: "unit",
                schema: "inventory",
                table: "stock_items",
                newName: "quantity_unit");

            migrationBuilder.AddColumn<int>(
                name: "expiry_month",
                schema: "inventory",
                table: "stock_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "expiry_year",
                schema: "inventory",
                table: "stock_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lot_code",
                schema: "inventory",
                table: "stock_items",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "quantity_amount",
                schema: "inventory",
                table: "stock_items",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            // Recreate the pre-batch current-state view (the E12 shape reading the restored item columns).
            migrationBuilder.Sql(
                """
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
    }
}
