using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds the permission codes that gate the Agenda module write endpoints (cards [E10] #69/#70/#71).
    /// Every <c>[RequirePermission]</c>-gated action on <c>RoomsController</c>, <c>BioteriumController</c>
    /// and <c>PresentationsController</c> must have its code seeded here; the Administrator profile
    /// receives all codes automatically via the Lumen auto-grant trigger.
    /// </summary>
    public partial class SeedAgendaPermissions : Migration
    {
        private const string AgendaGroup = "Agenda";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SeedLumenPermissionGroup(
                AgendaGroup,
                description: "Permissões do módulo de Agenda (salas, biotério e apresentações)");

            // Rooms (card [E10] #69)
            migrationBuilder.SeedLumenPermission("Rooms.Register",     "Cadastrar sala",              AgendaGroup);
            migrationBuilder.SeedLumenPermission("Rooms.CreateBooking", "Reservar sala",               AgendaGroup);
            migrationBuilder.SeedLumenPermission("Rooms.CancelBooking", "Cancelar reserva de sala",    AgendaGroup);

            // Bioterium (card [E10] #70)
            migrationBuilder.SeedLumenPermission("Bioterium.Generate", "Gerar escala semanal do biotério", AgendaGroup);
            migrationBuilder.SeedLumenPermission("Bioterium.Swap",     "Permutar responsável do biotério",  AgendaGroup);
            migrationBuilder.SeedLumenPermission("Bioterium.MarkDone", "Marcar biotério como realizado",    AgendaGroup);

            // Presentations (card [E10] #71)
            migrationBuilder.SeedLumenPermission("Presentations.Schedule",   "Agendar apresentação",         AgendaGroup);
            migrationBuilder.SeedLumenPermission("Presentations.Reschedule", "Remarcar apresentação",        AgendaGroup);
            migrationBuilder.SeedLumenPermission("Presentations.Cancel",     "Cancelar apresentação",        AgendaGroup);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback (additive and idempotent; removing would ungate live endpoints).
        }
    }
}
