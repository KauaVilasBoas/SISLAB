using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds the permission codes that gate the experiment-responsibility write endpoints (card [E11]): assigning
    /// the lead responsible and adding/removing step responsibles. Every <c>[RequirePermission]</c>-gated action
    /// must have its code seeded here or it would return 403 for everyone; the Administrator profile receives all
    /// codes automatically via the <c>trg_auto_grant_permission_to_administrator</c> trigger.
    ///
    /// <para>Codes follow the <c>{ControllerPrefix}.{ActionName}</c> convention Lumen derives at runtime — here the
    /// <c>ExperimentsController</c> actions AssignResponsible / AssignStepResponsible / RemoveStepResponsible.
    /// <see cref="MigrationBuilderExtensions.SeedLumenPermission"/> emits idempotent SQL, so re-applying is safe.
    /// No rollback: permission rows are additive and removing them on a downgrade would lock members out of live
    /// endpoints.</para>
    /// </summary>
    public partial class SeedExperimentResponsibilityPermissions : Migration
    {
        private const string ExperimentsGroup = "Experimentos";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SeedLumenPermission(
                "Experiments.AssignResponsible",
                "Definir responsável do experimento",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "Experiments.AssignStepResponsible",
                "Adicionar responsável de etapa",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "Experiments.RemoveStepResponsible",
                "Remover responsável de etapa",
                ExperimentsGroup);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback (additive and idempotent; removing would ungate live endpoints).
        }
    }
}
