using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds the two read permissions the Members &amp; Profiles screen (card [E7] #105) added on the backend:
    /// <c>Profiles.ListProfiles</c> (the "Profiles" tab) and <c>CompanyMembers.ListEnrichedMembers</c> (the
    /// "Members" tab, enriched with name/e-mail and assigned profiles).
    ///
    /// <para>Since Lumen.Authorization 3.0.0 does not auto-discover permissions, every <c>[RequirePermission]</c>
    /// action must have its <c>&lt;Controller&gt;.&lt;Action&gt;</c> code seeded here or it would 403 for
    /// everyone. Both codes reuse groups already created by <c>SeedPermissions</c> ("Perfis e Permissões" and
    /// "Membros da Empresa"); re-declaring a group is a no-op. The Administrator profile receives both codes
    /// automatically through the <c>trg_auto_grant_permission_to_administrator</c> trigger installed by
    /// <c>AutoGrantAdminPermissions</c>, so no companion grant step is needed. The
    /// <see cref="MigrationBuilderExtensions.SeedLumenPermission"/> helper emits idempotent SQL, so re-applying
    /// this migration is safe.</para>
    /// </summary>
    public partial class SeedProfileAndMemberReadPermissions : Migration
    {
        private const string ProfilesGroup = "Perfis e Permissões";
        private const string CompanyMembersGroup = "Membros da Empresa";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SeedLumenPermissionGroup(
                ProfilesGroup,
                description: "Permissões de gestão de perfis de autorização e sua atribuição a membros");

            migrationBuilder.SeedLumenPermission(
                "Profiles.ListProfiles", "Listar perfis", ProfilesGroup);

            migrationBuilder.SeedLumenPermissionGroup(
                CompanyMembersGroup,
                description: "Permissões de administração de membros da empresa");

            migrationBuilder.SeedLumenPermission(
                "CompanyMembers.ListEnrichedMembers", "Listar membros com nome e perfis", CompanyMembersGroup);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback (additive and idempotent; removing it would ungate live endpoints).
        }
    }
}
