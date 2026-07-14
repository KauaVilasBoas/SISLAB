using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Configuration.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "configuration");

            migrationBuilder.CreateTable(
                name: "expiry_policies",
                schema: "configuration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warning_window_days = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expiry_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "item_categories",
                schema: "configuration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    aliases = table.Column<string>(type: "text", nullable: false),
                    is_controlled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reference_ranges",
                schema: "configuration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    analyte = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    species = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    minimum = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    maximum = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reference_ranges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rooms",
                schema: "configuration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    requires_authorization = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rooms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "units",
                schema: "configuration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_units", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_expiry_policies_company_id",
                schema: "configuration",
                table: "expiry_policies",
                column: "company_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_categories_company_id_id",
                schema: "configuration",
                table: "item_categories",
                columns: new[] { "company_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_item_categories_company_id_name",
                schema: "configuration",
                table: "item_categories",
                columns: new[] { "company_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reference_ranges_company_id_id",
                schema: "configuration",
                table: "reference_ranges",
                columns: new[] { "company_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_reference_ranges_company_id_analyte_species",
                schema: "configuration",
                table: "reference_ranges",
                columns: new[] { "company_id", "analyte", "species" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rooms_company_id_id",
                schema: "configuration",
                table: "rooms",
                columns: new[] { "company_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_rooms_company_id_name",
                schema: "configuration",
                table: "rooms",
                columns: new[] { "company_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_units_company_id_id",
                schema: "configuration",
                table: "units",
                columns: new[] { "company_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_units_company_id_symbol",
                schema: "configuration",
                table: "units",
                columns: new[] { "company_id", "symbol" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expiry_policies",
                schema: "configuration");

            migrationBuilder.DropTable(
                name: "item_categories",
                schema: "configuration");

            migrationBuilder.DropTable(
                name: "reference_ranges",
                schema: "configuration");

            migrationBuilder.DropTable(
                name: "rooms",
                schema: "configuration");

            migrationBuilder.DropTable(
                name: "units",
                schema: "configuration");
        }
    }
}
