using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimalInclusionDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "inclusion_deciding_value",
                schema: "experiments",
                table: "project_animals",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "inclusion_parameter_code",
                schema: "experiments",
                table: "project_animals",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "inclusion_reason",
                schema: "experiments",
                table: "project_animals",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "inclusion_status",
                schema: "experiments",
                table: "project_animals",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "inclusion_deciding_value",
                schema: "experiments",
                table: "project_animals");

            migrationBuilder.DropColumn(
                name: "inclusion_parameter_code",
                schema: "experiments",
                table: "project_animals");

            migrationBuilder.DropColumn(
                name: "inclusion_reason",
                schema: "experiments",
                table: "project_animals");

            migrationBuilder.DropColumn(
                name: "inclusion_status",
                schema: "experiments",
                table: "project_animals");
        }
    }
}
