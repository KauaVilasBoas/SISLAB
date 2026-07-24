using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSolutionPreparations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_solution_preparations",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dose_amount_g_per_kg = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    group_weight_grams = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    relation_weight_grams = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    relation_microlitres_per_gram = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    compound_state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    density_g_per_ml = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    is_vehicle_only = table.Column<bool>(type: "boolean", nullable: false),
                    compound_mass_grams = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    compound_volume_microlitres = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    final_volume_microlitres = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    diluent_volume_microlitres = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    formula_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    formula_expression = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    prepared_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    prepared_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_solution_preparations", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_solution_preparations_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "experiments",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_solution_preparations_group_id",
                schema: "experiments",
                table: "project_solution_preparations",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_solution_preparations_project_id",
                schema: "experiments",
                table: "project_solution_preparations",
                column: "project_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_solution_preparations",
                schema: "experiments");
        }
    }
}
