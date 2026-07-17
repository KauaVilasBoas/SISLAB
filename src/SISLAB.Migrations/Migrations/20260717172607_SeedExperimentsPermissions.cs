using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds the permission group and codes that gate the Experiments module's write endpoints (decision
    /// card #68 — the in vitro viability slice). Every <c>[RequirePermission]</c>-gated action must have its
    /// code seeded here or it would return 403 for everyone; the Administrator profile receives all codes
    /// automatically via the <c>trg_auto_grant_permission_to_administrator</c> trigger.
    ///
    /// <para>Permission codes follow the <c>{ControllerPrefix}.{ActionName}</c> convention Lumen derives at
    /// runtime — here the <c>ExperimentsController</c> actions Create / DesignPlate / ImportReading / Calculate.
    /// <see cref="MigrationBuilderExtensions.SeedLumenPermission"/> emits idempotent SQL, so re-applying is safe.
    /// No rollback: permission rows are additive and removing them on a downgrade would lock members out of live
    /// endpoints.</para>
    /// </summary>
    public partial class SeedExperimentsPermissions : Migration
    {
        private const string ExperimentsGroup = "Experimentos";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SeedLumenPermissionGroup(
                ExperimentsGroup,
                description: "Permissões do módulo de experimentos (ensaios, placas e cálculos)");

            migrationBuilder.SeedLumenPermission(
                "Experiments.Create",
                "Criar experimento",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "Experiments.DesignPlate",
                "Desenhar placa do experimento",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "Experiments.ImportReading",
                "Importar leitura da placa",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "Experiments.Calculate",
                "Calcular viabilidade do experimento",
                ExperimentsGroup);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback (additive and idempotent; removing would ungate live endpoints).
        }
    }
}
