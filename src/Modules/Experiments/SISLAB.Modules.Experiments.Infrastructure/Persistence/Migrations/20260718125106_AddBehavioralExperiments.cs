using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBehavioralExperiments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "batch_id",
                schema: "experiments",
                table: "experiments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "project_id",
                schema: "experiments",
                table: "experiments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "behavioral_measurements",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    timepoint_label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    raw_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    recorded_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recorded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    experiment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_behavioral_measurements", x => x.id);
                    table.ForeignKey(
                        name: "FK_behavioral_measurements_experiments_experiment_id",
                        column: x => x.experiment_id,
                        principalSchema: "experiments",
                        principalTable: "experiments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_experiments_project_id",
                schema: "experiments",
                table: "experiments",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_behavioral_measurements_experiment_id",
                schema: "experiments",
                table: "behavioral_measurements",
                column: "experiment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "behavioral_measurements",
                schema: "experiments");

            migrationBuilder.DropIndex(
                name: "ix_experiments_project_id",
                schema: "experiments",
                table: "experiments");

            migrationBuilder.DropColumn(
                name: "batch_id",
                schema: "experiments",
                table: "experiments");

            migrationBuilder.DropColumn(
                name: "project_id",
                schema: "experiments",
                table: "experiments");
        }
    }
}
