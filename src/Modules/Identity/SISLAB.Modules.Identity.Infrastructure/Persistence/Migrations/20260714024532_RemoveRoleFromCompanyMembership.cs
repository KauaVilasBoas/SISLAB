using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Reverts the domain-role modelling error: SISLAB no longer stores a member role. A member's
    /// permissions in a company are owned by Lumen (profiles assigned to the user, scoped to the
    /// company), so the <c>role</c> column on <c>company_memberships</c> is dropped. This forward
    /// migration follows <c>AddRoleToCompanyMembership</c> in history rather than editing it, keeping
    /// the applied history coherent.
    /// </summary>
    public partial class RemoveRoleFromCompanyMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "role",
                schema: "tenancy",
                table: "company_memberships");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Symmetric with AddRoleToCompanyMembership: recreate the column back-filling any existing
            // rows with 'ReadOnly' (the least-privilege default that migration used).
            migrationBuilder.AddColumn<string>(
                name: "role",
                schema: "tenancy",
                table: "company_memberships",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "ReadOnly");
        }
    }
}
