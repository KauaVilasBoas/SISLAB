using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds the permission codes that gate the in vivo experimental-design write endpoints (card [E11] #73,
    /// decision F1 — <c>Project → Batch → Group → Animal</c>). Every <c>[RequirePermission]</c>-gated action on
    /// <c>ProjectsController</c> must have its code seeded here or it would return 403 for everyone; the
    /// Administrator profile receives all codes automatically via the
    /// <c>trg_auto_grant_permission_to_administrator</c> trigger.
    ///
    /// <para>Codes follow the <c>{ControllerPrefix}.{ActionName}</c> convention Lumen derives at runtime — here the
    /// <c>ProjectsController</c> actions Create / AddBatch / AddGroup / AddAnimal / StartBatch. They join the
    /// existing "Experimentos" permission group.
    /// <see cref="MigrationBuilderExtensions.SeedLumenPermission"/> emits idempotent SQL, so re-applying is safe.
    /// No rollback: permission rows are additive and removing them on a downgrade would lock members out of live
    /// endpoints.</para>
    /// </summary>
    public partial class SeedInVivoProjectPermissions : Migration
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
                "Projects.Create",
                "Criar projeto (delineamento in vivo)",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "Projects.AddBatch",
                "Adicionar leva ao projeto",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "Projects.AddGroup",
                "Adicionar grupo (dose) à leva",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "Projects.AddAnimal",
                "Cadastrar animal no grupo",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "Projects.StartBatch",
                "Iniciar leva (congela o desenho)",
                ExperimentsGroup);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback (additive and idempotent; removing would ungate live endpoints).
        }
    }
}
