using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds the permission code that gates the in vivo solution-preparation write endpoint (SISLAB-01): calculating
    /// and persisting a dose group's traceable preparation snapshot. Every <c>[RequirePermission]</c>-gated action
    /// must have its code seeded here or Lumen would return 403 for everyone; the Administrator profile receives all
    /// codes automatically via the <c>trg_auto_grant_permission_to_administrator</c> trigger.
    ///
    /// <para>The code follows the <c>{ControllerPrefix}.{ActionName}</c> convention Lumen derives at runtime — here
    /// the <c>ProjectsController.PrepareGroupSolution</c> action, joining the existing "Experimentos" permission
    /// group. The read-side listing (<c>ListPreparations</c>) is <c>[Authorize]</c> only, so it needs no code.
    /// <see cref="MigrationBuilderExtensions.SeedLumenPermission"/> emits idempotent SQL, so re-applying is safe. No
    /// rollback: permission rows are additive and removing them on a downgrade would lock members out of live
    /// endpoints.</para>
    /// </summary>
    public partial class SeedSolutionPreparationPermission : Migration
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
                "Projects.PrepareGroupSolution",
                "Calcular e registrar preparo de solução in vivo",
                ExperimentsGroup);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback (additive and idempotent; removing would ungate live endpoints).
        }
    }
}
