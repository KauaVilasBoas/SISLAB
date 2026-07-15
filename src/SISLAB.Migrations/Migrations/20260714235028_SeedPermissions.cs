using Lumen.Authorization.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Seeds SISLAB's permission catalogue into Lumen's tables (<c>"Lumen"."PermissionGroup"</c> /
    /// <c>"Lumen"."Permission"</c>). Since Lumen.Authorization 3.0.0 never populates permissions itself
    /// (it only creates the empty schema on boot), this migration is the single source of truth for every
    /// permission the product exposes — one entry per <c>[RequirePermission]</c>-gated action, plus the
    /// catalogued read codes and the Lumen backoffice codes that gate the admin console.
    ///
    /// <para>The <see cref="MigrationBuilderExtensions.SeedLumenPermissionGroup"/> /
    /// <see cref="MigrationBuilderExtensions.SeedLumenPermission"/> helpers emit idempotent SQL, so applying
    /// this migration is safe to repeat. The pt-BR <c>displayName</c> is the label the profile-management UI
    /// shows on each permission checkbox; the <c>groupName</c> ties the permission to its checkbox section.</para>
    /// </summary>
    public partial class SeedPermissions : Migration
    {
        // Permission groups (checkbox sections in the profile UI), with their pt-BR descriptions.
        private const string StockGroup = "Estoque";
        private const string ConfigurationGroup = "Configuração";
        private const string NotificationsGroup = "Notificações";
        private const string ProfilesGroup = "Perfis e Permissões";
        private const string CompanyMembersGroup = "Membros da Empresa";
        private const string AuditGroup = "Auditoria";
        private const string BackofficeGroup = "Lumen Backoffice";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            SeedStock(migrationBuilder);
            SeedConfiguration(migrationBuilder);
            SeedNotifications(migrationBuilder);
            SeedProfiles(migrationBuilder);
            SeedCompanyMembers(migrationBuilder);
            SeedAudit(migrationBuilder);
            SeedLumenBackoffice(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data — no rollback. Permission rows are additive and idempotent; removing them on a
            // downgrade would risk locking members out of endpoints that are still gated by these codes.
        }

        /// <summary>Estoque — Inventory write endpoints (stock, equipment, partners).</summary>
        private static void SeedStock(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SeedLumenPermissionGroup(
                StockGroup,
                description: "Permissões do módulo de estoque (itens, equipamentos e parceiros)");

            migrationBuilder.SeedLumenPermission("Stock.RegisterStockItem", "Cadastrar item de estoque", StockGroup);
            migrationBuilder.SeedLumenPermission("Stock.RegisterEntry", "Registrar entrada de estoque", StockGroup);
            migrationBuilder.SeedLumenPermission("Stock.RegisterConsumption", "Registrar consumo", StockGroup);
            migrationBuilder.SeedLumenPermission("Stock.Transfer", "Transferir estoque", StockGroup);
            migrationBuilder.SeedLumenPermission("Stock.Dispose", "Descartar estoque", StockGroup);
            migrationBuilder.SeedLumenPermission("Stock.RegisterCount", "Registrar contagem de estoque", StockGroup);

            migrationBuilder.SeedLumenPermission("Equipment.Register", "Cadastrar equipamento", StockGroup);
            migrationBuilder.SeedLumenPermission("Equipment.Update", "Editar equipamento", StockGroup);
            migrationBuilder.SeedLumenPermission("Equipment.ChangeStatus", "Alterar status do equipamento", StockGroup);
            migrationBuilder.SeedLumenPermission("Equipment.DefineCalibration", "Definir calibração", StockGroup);
            migrationBuilder.SeedLumenPermission("Equipment.RecordMaintenance", "Registrar manutenção", StockGroup);

            migrationBuilder.SeedLumenPermission("Partners.Register", "Cadastrar parceiro", StockGroup);
            migrationBuilder.SeedLumenPermission("Partners.Update", "Editar parceiro", StockGroup);
            migrationBuilder.SeedLumenPermission("Partners.Deactivate", "Desativar parceiro", StockGroup);
            migrationBuilder.SeedLumenPermission("Partners.Reactivate", "Reativar parceiro", StockGroup);
            migrationBuilder.SeedLumenPermission("Partners.RecordSample", "Registrar amostra", StockGroup);
            migrationBuilder.SeedLumenPermission("Partners.RemoveSample", "Remover amostra", StockGroup);
        }

        /// <summary>Configuração — per-tenant reference data write endpoints.</summary>
        private static void SeedConfiguration(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SeedLumenPermissionGroup(
                ConfigurationGroup,
                description: "Permissões de dados de referência da empresa (unidades, salas, categorias, políticas de validade)");

            migrationBuilder.SeedLumenPermission("Unit.Create", "Criar unidade de medida", ConfigurationGroup);
            migrationBuilder.SeedLumenPermission("Room.Create", "Criar sala", ConfigurationGroup);
            migrationBuilder.SeedLumenPermission("ReferenceRange.Create", "Criar faixa de referência", ConfigurationGroup);
            migrationBuilder.SeedLumenPermission("ItemCategory.Create", "Criar categoria de item", ConfigurationGroup);
            migrationBuilder.SeedLumenPermission("ExpiryPolicy.SetWarningWindow", "Definir janela de alerta de validade", ConfigurationGroup);
        }

        /// <summary>Notificações — alert/notification management.</summary>
        private static void SeedNotifications(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SeedLumenPermissionGroup(
                NotificationsGroup,
                description: "Permissões de gestão de notificações e alertas");

            migrationBuilder.SeedLumenPermission("Notifications.MarkAsRead", "Marcar notificação como lida", NotificationsGroup);
        }

        /// <summary>Perfis e Permissões — authorization-profile management and its assignment to members.</summary>
        private static void SeedProfiles(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SeedLumenPermissionGroup(
                ProfilesGroup,
                description: "Permissões de gestão de perfis de autorização e sua atribuição a membros");

            migrationBuilder.SeedLumenPermission("Profiles.ListAvailablePermissions", "Listar permissões disponíveis", ProfilesGroup);
            migrationBuilder.SeedLumenPermission("Profiles.CreateProfile", "Criar perfil", ProfilesGroup);
            migrationBuilder.SeedLumenPermission("Profiles.UpdateProfile", "Editar perfil", ProfilesGroup);
            migrationBuilder.SeedLumenPermission("Profiles.SetProfilePermissions", "Definir permissões do perfil", ProfilesGroup);

            migrationBuilder.SeedLumenPermission("MemberProfiles.AssignProfile", "Atribuir perfil a membro", ProfilesGroup);
            migrationBuilder.SeedLumenPermission("MemberProfiles.RemoveProfile", "Remover perfil de membro", ProfilesGroup);
        }

        /// <summary>Membros da Empresa — active-company member administration.</summary>
        private static void SeedCompanyMembers(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SeedLumenPermissionGroup(
                CompanyMembersGroup,
                description: "Permissões de administração de membros da empresa");

            migrationBuilder.SeedLumenPermission("CompanyMembers.ListMembers", "Listar membros da empresa", CompanyMembersGroup);
            migrationBuilder.SeedLumenPermission("CompanyMembers.CheckRemovalEligibility", "Verificar elegibilidade de remoção", CompanyMembersGroup);
        }

        /// <summary>Auditoria — compliance-trail read/export.</summary>
        private static void SeedAudit(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SeedLumenPermissionGroup(
                AuditGroup,
                description: "Permissões de consulta e exportação da trilha de auditoria");

            migrationBuilder.SeedLumenPermission("Audit.List", "Listar auditoria", AuditGroup);
            migrationBuilder.SeedLumenPermission("Audit.Export", "Exportar auditoria", AuditGroup);
        }

        /// <summary>
        /// Lumen Backoffice — the permission codes Lumen's own admin console (<c>MapLumenBackoffice</c>) gates.
        /// Seeded so an administrator holding these codes can reach the console (view/manage profiles,
        /// permissions, groups and profile assignments).
        /// </summary>
        private static void SeedLumenBackoffice(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SeedLumenPermissionGroup(
                BackofficeGroup,
                description: "Permissões do console administrativo da Lumen (perfis, permissões e grupos)");

            migrationBuilder.SeedLumenPermission("Lumen.Profiles.View", "Ver perfis (Lumen)", BackofficeGroup);
            migrationBuilder.SeedLumenPermission("Lumen.Profiles.Manage", "Gerenciar perfis (Lumen)", BackofficeGroup);
            migrationBuilder.SeedLumenPermission("Lumen.Permissions.View", "Ver permissões (Lumen)", BackofficeGroup);
            migrationBuilder.SeedLumenPermission("Lumen.Permissions.Manage", "Gerenciar permissões (Lumen)", BackofficeGroup);
            migrationBuilder.SeedLumenPermission("Lumen.Groups.Manage", "Gerenciar grupos de permissão (Lumen)", BackofficeGroup);
            migrationBuilder.SeedLumenPermission("Lumen.UserProfiles.Manage", "Gerenciar atribuições de perfil (Lumen)", BackofficeGroup);
        }
    }
}
