using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds the permission codes that gate the collection-plan write endpoints (SISLAB-08 — the batch collection
    /// plan): creating the plan, defining/removing a sample type's routing, and assigning/removing a member's
    /// collection role. Every <c>[RequirePermission]</c>-gated action on <c>CollectionPlansController</c> must have
    /// its code seeded here or it would return 403 for everyone; the Administrator profile receives all codes
    /// automatically via the <c>trg_auto_grant_permission_to_administrator</c> trigger.
    ///
    /// <para>Codes follow the <c>{ControllerPrefix}.{ActionName}</c> convention Lumen derives at runtime — here the
    /// <c>CollectionPlansController</c> actions Create / DefineRouting / RemoveRouting / AssignRole / RemoveRole.
    /// They join the existing "Experimentos" permission group.
    /// <see cref="MigrationBuilderExtensions.SeedLumenPermission"/> emits idempotent SQL, so re-applying is safe.
    /// No rollback: permission rows are additive and removing them on a downgrade would lock members out of live
    /// endpoints.</para>
    /// </summary>
    public partial class SeedCollectionPlanPermissions : Migration
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
                "CollectionPlans.Create",
                "Criar plano de coleta da leva",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "CollectionPlans.DefineRouting",
                "Definir roteamento de tipo de amostra",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "CollectionPlans.RemoveRouting",
                "Remover roteamento de tipo de amostra",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "CollectionPlans.AssignRole",
                "Atribuir função de coleta a um membro",
                ExperimentsGroup);

            migrationBuilder.SeedLumenPermission(
                "CollectionPlans.RemoveRole",
                "Remover atribuição de função de coleta",
                ExperimentsGroup);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback (additive and idempotent; removing would ungate live endpoints).
        }
    }
}
