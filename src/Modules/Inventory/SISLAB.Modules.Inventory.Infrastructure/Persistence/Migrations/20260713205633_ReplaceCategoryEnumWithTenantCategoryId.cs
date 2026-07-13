using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migrates the stock item category from the retired closed <c>StockItemCategory</c> enum (a
    /// <c>character varying(40)</c> column) to a per-tenant category referenced <b>by value</b> — a plain
    /// <c>category_id uuid</c> pointing at a <c>configuration.item_categories</c> row (card [E12] #76). The
    /// category catalogue is now owned by the Configuration bounded context; Inventory keeps only the id, and
    /// the "category exists and belongs to the tenant" invariant is enforced on the write-side command via
    /// <c>ILabConfiguration</c>, never by a cross-module FK/navigation (module isolation, section 2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read-side (<c>inventory.stock_view</c>).</b> The current-state view projected the old free-text
    /// <c>si.category</c>; the read models (item listing/detail, consumption report, expiry/below-minimum) all
    /// expose the category by <b>name</b>. To keep that contract unchanged after the write column became an id,
    /// the view is recreated to resolve the human-readable name through a tenant-safe LEFT JOIN onto
    /// <c>configuration.item_categories</c> (<c>ON ic.id = si.category_id AND ic.company_id = si.company_id</c>),
    /// projecting <c>ic.name AS category</c> — exactly how the view already resolves <c>storage_location_name</c>.
    /// This is a physical read-side join across schemas of the same database (the Dapper read side reads one
    /// database), not a compile-time cross-module reference, so it does not breach module isolation: every
    /// downstream read query keeps selecting the view's <c>category</c> column unchanged, and also gains
    /// <c>category_id</c> for id-based filtering.
    /// </para>
    /// <para>
    /// <b>Ordering.</b> The view depends on the <c>category</c> column, so it is dropped before the column is
    /// dropped and recreated after <c>category_id</c> is added. The swap is a plain drop+add (no backfill): the
    /// database is provisioned from migrations and no stock items are seeded, so there is no legacy enum data to
    /// translate; a future tenant with data would backfill via a data migration mapping legacy names to ids.
    /// </para>
    /// </remarks>
    /// <inheritdoc />
    public partial class ReplaceCategoryEnumWithTenantCategoryId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The view reads si.category; drop it before the column it depends on.
            migrationBuilder.Sql("DROP VIEW IF EXISTS inventory.stock_view;");

            migrationBuilder.DropColumn(
                name: "category",
                schema: "inventory",
                table: "stock_items");

            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                schema: "inventory",
                table: "stock_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Recreate the current-state view resolving the category name from the Configuration catalogue.
            // The join carries company_id so it can never resolve a name across tenants; category_id is also
            // projected so read queries can filter by id when the name is not enough.
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS inventory.stock_view;");

            migrationBuilder.DropColumn(
                name: "category_id",
                schema: "inventory",
                table: "stock_items");

            migrationBuilder.AddColumn<string>(
                name: "category",
                schema: "inventory",
                table: "stock_items",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            // Restore the pre-E12 view that projected the free-text category column.
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
    }
}
