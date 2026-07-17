using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds the <c>Inventory.Cost.Read</c> permission (card [E4] #109), which gates the two cost-report
    /// endpoints (<c>GET /api/inventory/reports/cost-by-month</c> and <c>/cost-by-experiment</c>). Cost is
    /// gestão-sensitive data — not every profile may see how much the laboratory spends — so the endpoints are
    /// marked <c>[RequirePermission("Inventory.Cost.Read")]</c> and the code must be seeded here or they would
    /// return 403 for everyone; the Administrator profile receives it automatically via the
    /// <c>trg_auto_grant_permission_to_administrator</c> trigger.
    ///
    /// <para>Unlike the other stock endpoints (whose codes follow Lumen's runtime <c>{Controller}.{Action}</c>
    /// convention), this is an explicit, feature-level code shared by both cost endpoints — a single "view the
    /// cost report" capability. <see cref="MigrationBuilderExtensions.SeedLumenPermission"/> emits idempotent
    /// SQL, so re-applying is safe. No rollback: permission rows are additive and removing them on a downgrade
    /// would lock members out of live endpoints.</para>
    /// </summary>
    public partial class SeedInventoryCostReadPermission : Migration
    {
        private const string StockGroup = "Estoque";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Re-declare the group (idempotent no-op if it already exists).
            migrationBuilder.SeedLumenPermissionGroup(
                StockGroup,
                description: "Permissões do módulo de estoque (itens, equipamentos e parceiros)");

            migrationBuilder.SeedLumenPermission(
                "Inventory.Cost.Read",
                "Visualizar relatório de custo",
                StockGroup);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback (additive and idempotent; removing would ungate live endpoints).
        }
    }
}
