using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "partners",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    document = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partners", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "partner_samples",
                schema: "inventory",
                columns: table => new
                {
                    reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partner_samples", x => new { x.partner_id, x.reference });
                    table.ForeignKey(
                        name: "FK_partner_samples_partners_partner_id",
                        column: x => x.partner_id,
                        principalSchema: "inventory",
                        principalTable: "partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_partners_company_id_id",
                schema: "inventory",
                table: "partners",
                columns: new[] { "company_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "partner_samples",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "partners",
                schema: "inventory");
        }
    }
}
