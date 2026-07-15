using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds the <c>Stock.ListStockMovements</c> read permission the movements-history screen (card [E7] #47)
    /// added on the backend. It gates the ledger endpoint
    /// (<c>GET api/inventory/stock-items/{stockItemId}/movements</c>) — a member without this code in the active
    /// company gets 403.
    ///
    /// <para>Since Lumen.Authorization 3.0.0 does not auto-discover permissions, every <c>[RequirePermission]</c>
    /// action must have its <c>&lt;Controller&gt;.&lt;Action&gt;</c> code seeded here or it would 403 for
    /// everyone. The code reuses the "Estoque" group already created by <c>SeedPermissions</c>; re-declaring a
    /// group is a no-op. The Administrator profile receives the code automatically through the
    /// <c>trg_auto_grant_permission_to_administrator</c> trigger installed by <c>AutoGrantAdminPermissions</c>,
    /// so no companion grant step is needed. The <see cref="MigrationBuilderExtensions.SeedLumenPermission"/>
    /// helper emits idempotent SQL, so re-applying this migration is safe.</para>
    /// </summary>
    public partial class SeedListStockMovementsPermission : Migration
    {
        private const string StockGroup = "Estoque";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SeedLumenPermissionGroup(
                StockGroup,
                description: "Permissões do módulo de estoque (itens, equipamentos e parceiros)");

            migrationBuilder.SeedLumenPermission(
                "Stock.ListStockMovements", "Consultar movimentações de estoque", StockGroup);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback (additive and idempotent; removing it would ungate a live endpoint).
        }
    }
}
