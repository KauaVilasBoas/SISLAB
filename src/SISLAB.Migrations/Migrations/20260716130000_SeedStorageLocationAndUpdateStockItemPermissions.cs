using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds permissions for the two write-side endpoints added in card [E7] #112 (storage location CRUD) and
    /// card [E7] #46 (stock item metadata update). Every <c>[RequirePermission]</c>-gated action must have its
    /// code seeded here or it would return 403 for everyone; the Administrator profile receives all codes
    /// automatically via the <c>trg_auto_grant_permission_to_administrator</c> trigger.
    ///
    /// <para>Permission codes follow the <c>{ControllerPrefix}.{ActionName}</c> convention that Lumen derives at
    /// runtime. <see cref="MigrationBuilderExtensions.SeedLumenPermission"/> emits idempotent SQL, so re-applying
    /// is safe. No rollback: permission rows are additive and removing them on a downgrade would lock members out
    /// of live endpoints.</para>
    /// </summary>
    public partial class SeedStorageLocationAndUpdateStockItemPermissions : Migration
    {
        private const string StockGroup = "Estoque";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Re-declare the group (idempotent no-op if it already exists).
            migrationBuilder.SeedLumenPermissionGroup(
                StockGroup,
                description: "Permissões do módulo de estoque (itens, equipamentos e parceiros)");

            // Stock item metadata update — PUT /api/inventory/stock-items/{id} (E7 #46)
            migrationBuilder.SeedLumenPermission(
                "Stock.UpdateStockItem",
                "Editar metadados do item de estoque",
                StockGroup);

            // Storage location CRUD — StorageLocationsController (E7 #112)
            migrationBuilder.SeedLumenPermission(
                "StorageLocations.Register",
                "Cadastrar local de armazenamento",
                StockGroup);

            migrationBuilder.SeedLumenPermission(
                "StorageLocations.Update",
                "Editar local de armazenamento",
                StockGroup);

            migrationBuilder.SeedLumenPermission(
                "StorageLocations.ChangeStatus",
                "Ativar/desativar local de armazenamento",
                StockGroup);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback (additive and idempotent; removing would ungate live endpoints).
        }
    }
}
