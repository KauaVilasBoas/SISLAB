using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds the permission codes that gate the improved-calendar write endpoints (card [E10.3] #3). Each
    /// <c>[RequirePermission]</c>-gated action on <c>AgendaEntriesController</c> resolves the implicit code
    /// <c>AgendaEntries.{Action}</c>; every such code must be seeded here so the enforcement filter can match
    /// it. The Administrator profile receives all codes automatically via the Lumen auto-grant trigger.
    /// </summary>
    public partial class SeedAgendaEntryPermissions : Migration
    {
        private const string AgendaGroup = "Agenda";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reuse the Agenda group seeded by SeedAgendaPermissions (idempotent).
            migrationBuilder.SeedLumenPermissionGroup(
                AgendaGroup,
                description: "Permissões do módulo de Agenda (salas, biotério e apresentações)");

            migrationBuilder.SeedLumenPermission("AgendaEntries.Create",           "Criar evento da agenda",        AgendaGroup);
            migrationBuilder.SeedLumenPermission("AgendaEntries.Update",           "Editar evento da agenda",       AgendaGroup);
            migrationBuilder.SeedLumenPermission("AgendaEntries.CancelOccurrence", "Cancelar ocorrência de evento", AgendaGroup);
            migrationBuilder.SeedLumenPermission("AgendaEntries.Delete",           "Excluir evento da agenda",      AgendaGroup);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback (additive and idempotent; removing would ungate live endpoints).
        }
    }
}
