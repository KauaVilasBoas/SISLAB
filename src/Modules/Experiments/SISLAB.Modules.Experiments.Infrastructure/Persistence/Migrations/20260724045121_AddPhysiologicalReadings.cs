using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhysiologicalReadings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_physiological_readings",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parameter_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    timepoint_label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    recorded_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recorded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_physiological_readings", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_physiological_readings_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "experiments",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_physiological_readings_animal_id",
                schema: "experiments",
                table: "project_physiological_readings",
                column: "animal_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_physiological_readings_project_id",
                schema: "experiments",
                table: "project_physiological_readings",
                column: "project_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_physiological_readings",
                schema: "experiments");
        }
    }
}
