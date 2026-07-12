using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Read-side (E4 #33) desnormalized sources, both in the <c>inventory</c> schema:
    /// <list type="bullet">
    ///   <item>
    ///     <c>stock_view</c> — a SQL view flattening the current state of each item (join of
    ///     <c>stock_items</c> and its <c>storage_locations</c>), carrying <c>company_id</c>. It is the
    ///     read source for the item/stock listing queries (cards #29/#30/#32). Chosen as a plain view
    ///     over the write tables: the LAFTE volume does not justify an event-projected table for the
    ///     current-state read, and it is always consistent with the write side by construction.
    ///   </item>
    ///   <item>
    ///     <c>stock_movements</c> — a projected table, one row per stock movement, fed asynchronously by
    ///     <c>StockMovementProjectionHandler</c> from the Outbox integration events. The write side does
    ///     not persist movements (only the current balance), so this table is the read model that
    ///     reconstructs the ledger the consumption report (card #31) needs, with the operator-supplied
    ///     traceability metadata (occurred_on, experiment_id, partner_id).
    ///   </item>
    /// </list>
    /// These objects are NOT mapped as EF entities (they are read via Dapper and written via the
    /// projection's raw SQL), so the model snapshot is intentionally unchanged — the migration carries
    /// the DDL as raw SQL / a table create that the EF model does not track.
    /// </summary>
    /// <inheritdoc />
    public partial class AddInventoryReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Projected movements table (event-sourced read model) ------------------------------
            // id == the originating integration event's EventId: one event → one row. The primary key
            // gives idempotency for free — reprocessing the same Outbox message hits ON CONFLICT (id)
            // and does not duplicate the movement (card [E4] #33 acceptance criterion).
            migrationBuilder.CreateTable(
                name: "stock_movements",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    quantity_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    occurred_on = table.Column<DateOnly>(type: "date", nullable: true),
                    experiment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    // performed_by (responsável) is intentionally left unpopulated for now: the Inventory
                    // module has no UserId — only CompanyId via ITenantContext (decision on card [E3]
                    // #24). The column exists so the audit trail (card #57 / E9), which owns the operator
                    // identity, can backfill it without a schema change.
                    performed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_movements", x => x.id);
                });

            // Report/listing access paths: by company over time (the ledger), and by company + item.
            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_company_id_occurred_on",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "company_id", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_company_id_stock_item_id",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "company_id", "stock_item_id" });

            // --- Current-state view (flattened item × storage location) ----------------------------
            // Columns are lowercase/snake_case to match the read-side Dapper convention. company_id is
            // projected so every tenant-scoped SELECT can keep its WHERE company_id = @CompanyId.
            migrationBuilder.Sql(
                """
                CREATE VIEW inventory.stock_view AS
                SELECT
                    si.id                       AS id,
                    si.company_id               AS company_id,
                    si.name                     AS name,
                    si.category                 AS category,
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
                   AND sl.company_id = si.company_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS inventory.stock_view;");

            migrationBuilder.DropTable(
                name: "stock_movements",
                schema: "inventory");
        }
    }
}
