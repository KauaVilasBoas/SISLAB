using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the outlier-exclusion columns to <c>experiments.wells</c> (SISLAB-06): <c>is_excluded</c> (defaulting
    /// to false so existing wells migrate cleanly), plus <c>exclusion_reason</c> and <c>excluded_by</c> for the
    /// operator's traceable, human decision to drop a replicate before the calculation runs.
    /// </summary>
    /// <inheritdoc />
    public partial class AddWellOutlierExclusion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "excluded_by",
                schema: "experiments",
                table: "wells",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exclusion_reason",
                schema: "experiments",
                table: "wells",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_excluded",
                schema: "experiments",
                table: "wells",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "excluded_by",
                schema: "experiments",
                table: "wells");

            migrationBuilder.DropColumn(
                name: "exclusion_reason",
                schema: "experiments",
                table: "wells");

            migrationBuilder.DropColumn(
                name: "is_excluded",
                schema: "experiments",
                table: "wells");
        }
    }
}
