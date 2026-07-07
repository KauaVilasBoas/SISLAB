using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTenancySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenancy");

            migrationBuilder.CreateTable(
                name: "companies",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tax_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "company_memberships",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lumen_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_memberships", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_memberships_companies_company_id",
                        column: x => x.company_id,
                        principalSchema: "tenancy",
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_companies_name",
                schema: "tenancy",
                table: "companies",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_company_memberships_company_user",
                schema: "tenancy",
                table: "company_memberships",
                columns: new[] { "company_id", "lumen_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_company_memberships_lumen_user_id",
                schema: "tenancy",
                table: "company_memberships",
                column: "lumen_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_memberships",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "companies",
                schema: "tenancy");
        }
    }
}
