using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds the permission codes that gate the Configuration module's cadaster write endpoints added in this
    /// slice: the experimental model (SISLAB-04), the animal-inclusion criterion (SISLAB-02) and the collection
    /// role (SISLAB-08). Every <c>[RequirePermission]</c>-gated action must have its code seeded here or it would
    /// return 403 for everyone; the Administrator profile receives all codes automatically via the
    /// <c>trg_auto_grant_permission_to_administrator</c> trigger.
    ///
    /// <para>Codes follow the <c>{ControllerPrefix}.{ActionName}</c> convention Lumen derives at runtime — here the
    /// <c>ExperimentalModelController</c> / <c>InclusionCriterionController</c> / <c>CollectionRoleController</c>
    /// Create actions. They join the existing "Configuração" permission group alongside the other reference-data
    /// cadaster codes. <see cref="MigrationBuilderExtensions.SeedLumenPermission"/> emits idempotent SQL, so
    /// re-applying is safe. No rollback: permission rows are additive and removing them on a downgrade would lock
    /// members out of live endpoints.</para>
    /// </summary>
    public partial class SeedConfigurationCadasterPermissions : Migration
    {
        private const string ConfigurationGroup = "Configuração";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Re-declare the group (idempotent no-op if it already exists).
            migrationBuilder.SeedLumenPermissionGroup(
                ConfigurationGroup,
                description: "Permissões de dados de referência da empresa (unidades, salas, categorias, políticas de validade)");

            migrationBuilder.SeedLumenPermission(
                "ExperimentalModel.Create",
                "Cadastrar modelo experimental",
                ConfigurationGroup);

            migrationBuilder.SeedLumenPermission(
                "InclusionCriterion.Create",
                "Cadastrar critério de inclusão",
                ConfigurationGroup);

            migrationBuilder.SeedLumenPermission(
                "CollectionRole.Create",
                "Cadastrar função de coleta",
                ConfigurationGroup);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback (additive and idempotent; removing would ungate live endpoints).
        }
    }
}
