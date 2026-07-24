using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds the permission codes that gate the in vivo project write endpoints introduced after the initial
    /// <c>SeedInVivoProjectPermissions</c> slice: housing cages (SISLAB-03), assigning an animal to a group
    /// post-randomization (SISLAB-03), recording a physiological reading (SISLAB-02), applying the inclusion
    /// criteria to a batch (SISLAB-02) and binding a batch to an experimental model (SISLAB-04). Every
    /// <c>[RequirePermission]</c>-gated action on <c>ProjectsController</c> must have its code seeded here or it
    /// would return 403 for everyone; the Administrator profile receives all codes automatically via the
    /// <c>trg_auto_grant_permission_to_administrator</c> trigger.
    ///
    /// <para>Codes follow the <c>{ControllerPrefix}.{ActionName}</c> convention Lumen derives at runtime — here the
    /// <c>ProjectsController</c> actions AddCage / AssignAnimalToGroup / RecordReading / ApplySelection /
    /// BindBatchModel. They join the existing "Experimentos" permission group.
    /// <see cref="MigrationBuilderExtensions.SeedLumenPermission"/> emits idempotent SQL, so re-applying is safe.
    /// No rollback: permission rows are additive and removing them on a downgrade would lock members out of live
    /// endpoints.</para>
    /// </summary>
    public partial class SeedInVivoProjectExtraPermissions : Migration
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
                "Projects.AddCage",
                "Adicionar caixa (gaiola) à leva",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "Projects.AssignAnimalToGroup",
                "Atribuir animal a um grupo",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "Projects.RecordReading",
                "Registrar leitura fisiológica",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "Projects.ApplySelection",
                "Aplicar critérios de inclusão à leva",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "Projects.BindBatchModel",
                "Vincular leva a um modelo experimental",
                ExperimentsGroup);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback (additive and idempotent; removing would ungate live endpoints).
        }
    }
}
