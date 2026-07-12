using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SISLAB.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "equipment",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    asset_tag = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    storage_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    last_calibration = table.Column<DateOnly>(type: "date", nullable: true),
                    next_calibration = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "equipment_maintenances",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_maintenances", x => x.id);
                    table.ForeignKey(
                        name: "FK_equipment_maintenances_equipment_equipment_id",
                        column: x => x.equipment_id,
                        principalSchema: "inventory",
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_equipment_company_id_id",
                schema: "inventory",
                table: "equipment",
                columns: new[] { "company_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_equipment_maintenances_equipment_id",
                schema: "inventory",
                table: "equipment_maintenances",
                column: "equipment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipment_maintenances",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "equipment",
                schema: "inventory");
        }
    }
}
