using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds the <c>Notifications.ReadAll</c> write permission the "marcar todas como lidas" endpoint (card
    /// [E7] #65) added on the backend. It gates <c>POST api/notifications/read-all</c> — a member without this
    /// code in the active company gets 403, symmetrically to <c>Notifications.MarkAsRead</c>.
    ///
    /// <para>Since Lumen.Authorization 3.0.0 does not auto-discover permissions, every <c>[RequirePermission]</c>
    /// action must have its <c>&lt;Controller&gt;.&lt;Action&gt;</c> code seeded here or it would 403 for
    /// everyone. The code reuses the "Notificações" group already created by <c>SeedPermissions</c>; re-declaring
    /// a group is a no-op. The Administrator profile receives the code automatically through the
    /// <c>trg_auto_grant_permission_to_administrator</c> trigger installed by <c>AutoGrantAdminPermissions</c>,
    /// so no companion grant step is needed. The <see cref="MigrationBuilderExtensions.SeedLumenPermission"/>
    /// helper emits idempotent SQL, so re-applying this migration is safe.</para>
    /// </summary>
    public partial class SeedReadAllNotificationsPermission : Migration
    {
        private const string NotificationsGroup = "Notificações";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SeedLumenPermissionGroup(
                NotificationsGroup,
                description: "Permissões de gestão de notificações e alertas");

            migrationBuilder.SeedLumenPermission(
                "Notifications.ReadAll", "Marcar todas as notificações como lidas", NotificationsGroup);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback (additive and idempotent; removing it would ungate a live endpoint).
        }
    }
}
