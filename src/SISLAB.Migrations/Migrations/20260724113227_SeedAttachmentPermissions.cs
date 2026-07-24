using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds the permission code that gates the evidence-attachment write endpoint (SISLAB-09 — the
    /// <c>Attachment</c> aggregate). The <c>[RequirePermission]</c>-gated <c>AttachmentsController.Attach</c>
    /// action must have its code seeded here or it would return 403 for everyone; the Administrator profile
    /// receives all codes automatically via the <c>trg_auto_grant_permission_to_administrator</c> trigger.
    ///
    /// <para>The code follows the <c>{ControllerPrefix}.{ActionName}</c> convention Lumen derives at runtime — here
    /// the <c>AttachmentsController</c> action Attach. It joins the existing "Experimentos" permission group. The
    /// list/download reads are page-level <c>[Authorize]</c>, not permission-gated, so they need no code.
    /// <see cref="MigrationBuilderExtensions.SeedLumenPermission"/> emits idempotent SQL, so re-applying is safe. No
    /// rollback: permission rows are additive and removing them on a downgrade would lock members out of a live
    /// endpoint.</para>
    /// </summary>
    public partial class SeedAttachmentPermissions : Migration
    {
        private const string ExperimentsGroup = "Experimentos";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Re-declare the group (idempotent no-op if it already exists).
            migrationBuilder.SeedLumenPermissionGroup(
                ExperimentsGroup,
                description: "Permissões do módulo de experimentos (ensaios, placas e cálculos)");

            migrationBuilder.SeedLumenPermission(
                "Attachments.Attach",
                "Anexar evidência a uma leitura/análise",
                ExperimentsGroup);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback (additive and idempotent; removing would ungate a live endpoint).
        }
    }
}
