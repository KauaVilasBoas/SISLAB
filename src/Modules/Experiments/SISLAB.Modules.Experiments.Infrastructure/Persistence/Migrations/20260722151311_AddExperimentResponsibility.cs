using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExperimentResponsibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "responsible_user_id",
                schema: "experiments",
                table: "experiments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "experiment_step_responsibles",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experiment_step_responsibles", x => x.id);
                    table.ForeignKey(
                        name: "FK_experiment_step_responsibles_experiment_steps_step_id",
                        column: x => x.step_id,
                        principalSchema: "experiments",
                        principalTable: "experiment_steps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_experiment_step_responsibles_step_id_user_id",
                schema: "experiments",
                table: "experiment_step_responsibles",
                columns: new[] { "step_id", "user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "experiment_step_responsibles",
                schema: "experiments");

            migrationBuilder.DropColumn(
                name: "responsible_user_id",
                schema: "experiments",
                table: "experiments");
        }
    }
}
