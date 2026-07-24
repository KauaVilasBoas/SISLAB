using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Configuration.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInclusionCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inclusion_criteria",
                schema: "configuration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parameter_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    @operator = table.Column<string>(name: "operator", type: "character varying(30)", maxLength: 30, nullable: false),
                    threshold = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inclusion_criteria", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inclusion_criteria_company_id_id",
                schema: "configuration",
                table: "inclusion_criteria",
                columns: new[] { "company_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_inclusion_criteria_company_id_parameter_code",
                schema: "configuration",
                table: "inclusion_criteria",
                columns: new[] { "company_id", "parameter_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inclusion_criteria",
                schema: "configuration");
        }
    }
}
