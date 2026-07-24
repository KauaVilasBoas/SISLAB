using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Configuration.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExperimentalModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "experimental_models",
                schema: "configuration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    induction_administrations = table.Column<int>(type: "integer", nullable: false),
                    induction_interval_days = table.Column<int>(type: "integer", nullable: false),
                    induction_reference_day = table.Column<int>(type: "integer", nullable: false),
                    timepoints = table.Column<string>(type: "jsonb", nullable: false),
                    parameters = table.Column<string>(type: "jsonb", nullable: false),
                    groups = table.Column<string>(type: "jsonb", nullable: false),
                    ratio_microlitres_per_gram = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    default_diluent = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_experimental_models", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_experimental_models_company_id_id",
                schema: "configuration",
                table: "experimental_models",
                columns: new[] { "company_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_experimental_models_company_id_name",
                schema: "configuration",
                table: "experimental_models",
                columns: new[] { "company_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "experimental_models",
                schema: "configuration");
        }
    }
}
