using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleToCompanyMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the role column non-nullable, back-filling any pre-existing membership rows with
            // 'ReadOnly' (least privilege) via a temporary column default. The default is dropped
            // immediately afterwards so the column carries no server default going forward: the
            // application always writes Role explicitly through the Company aggregate, which keeps
            // the schema aligned with the EF model (no model-level HasDefaultValue).
            migrationBuilder.AddColumn<string>(
                name: "role",
                schema: "tenancy",
                table: "company_memberships",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "ReadOnly");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                schema: "tenancy",
                table: "company_memberships",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValue: "ReadOnly");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "role",
                schema: "tenancy",
                table: "company_memberships");
        }
    }
}
