using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds the permission codes that gate the plate outlier-management write endpoints (SISLAB-06): excluding a
    /// well as an outlier before the calculation runs, and bringing a previously excluded well back in. Every
    /// <c>[RequirePermission]</c>-gated action on <c>ExperimentsController</c> must have its code seeded here or it
    /// would return 403 for everyone; the Administrator profile receives all codes automatically via the
    /// <c>trg_auto_grant_permission_to_administrator</c> trigger.
    ///
    /// <para>Codes follow the <c>{ControllerPrefix}.{ActionName}</c> convention Lumen derives at runtime — here the
    /// <c>ExperimentsController</c> actions ExcludeWell / IncludeWell. They join the existing "Experimentos"
    /// permission group. <see cref="MigrationBuilderExtensions.SeedLumenPermission"/> emits idempotent SQL, so
    /// re-applying is safe. No rollback: permission rows are additive and removing them on a downgrade would lock
    /// members out of live endpoints.</para>
    /// </summary>
    public partial class SeedExperimentWellExclusionPermissions : Migration
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
                "Experiments.ExcludeWell",
                "Excluir poço da placa (outlier)",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "Experiments.IncludeWell",
                "Reincluir poço da placa",
                ExperimentsGroup);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback (additive and idempotent; removing would ungate live endpoints).
        }
    }
}
