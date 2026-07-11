using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialInventorySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    occurred_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_items",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    container_state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    application = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_controlled = table.Column<bool>(type: "boolean", nullable: false),
                    storage_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    expiry_year = table.Column<int>(type: "integer", nullable: true),
                    expiry_month = table.Column<int>(type: "integer", nullable: true),
                    minimum_quantity_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    minimum_quantity_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    quantity_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "storage_locations",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    temp_min = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    temp_max = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_storage_locations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_on_utc",
                schema: "inventory",
                table: "outbox_messages",
                column: "occurred_on_utc");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at_utc",
                schema: "inventory",
                table: "outbox_messages",
                column: "processed_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_stock_items_company_id_id",
                schema: "inventory",
                table: "stock_items",
                columns: new[] { "company_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_storage_locations_company_id_id",
                schema: "inventory",
                table: "storage_locations",
                columns: new[] { "company_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_items",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "storage_locations",
                schema: "inventory");
        }
    }
}
