using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds the <c>CompanyMembers.InviteMember</c> permission into Lumen's catalogue (card [E12] #75c), in the
    /// existing "Membros da Empresa" group. It gates the coordinator's invite endpoint
    /// (<c>POST api/admin/companies/active/members/invite</c>) — a member without this code in the active company
    /// gets 403.
    ///
    /// <para>Uses the same idempotent <see cref="MigrationBuilderExtensions.SeedLumenPermission"/> helper as the
    /// base <c>SeedPermissions</c> migration, so re-applying is safe. The group already exists (seeded by
    /// <c>SeedPermissions</c>); re-seeding it is a no-op. The Administrator profile receives the new permission
    /// automatically via the <c>trg_auto_grant_permission_to_administrator</c> trigger installed by
    /// <c>AutoGrantAdminPermissions</c>, so no companion grant step is needed.</para>
    /// </summary>
    public partial class SeedInviteMemberPermission : Migration
    {
        private const string CompanyMembersGroup = "Membros da Empresa";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: re-declaring the group is a no-op; the new permission is granted to Administrator by trigger.
            migrationBuilder.SeedLumenPermissionGroup(
                CompanyMembersGroup,
                description: "Permissões de administração de membros da empresa");

            migrationBuilder.SeedLumenPermission(
                "CompanyMembers.InviteMember", "Convidar membro por e-mail", CompanyMembersGroup);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback (additive and idempotent; removing it would ungate a live endpoint).
        }
    }
}
