using SISLAB.SharedKernel.Authorization;

namespace SISLAB.Modules.Identity.Infrastructure.Authorization;

/// <summary>
/// Portuguese (pt-BR) display-name catalogue for every permission code SISLAB exposes through Lumen's
/// <c>[RequirePermission]</c> discovery. It maps each <c>&lt;Controller&gt;.&lt;Action&gt;</c> code to the
/// human-readable label shown in the profile-management UI (the permission checkboxes).
///
/// <para><b>Why here, and why a seeder instead of a migration.</b> Lumen owns the <c>"Lumen"."Permission"</c>
/// table and re-materializes/normalizes it on every boot via <c>AddLumenAuthorizationDiscovery()</c> — a
/// migration seeding <c>DisplayName</c> would be overwritten by that discovery. Instead
/// <see cref="PermissionDisplayNameSeeder"/> runs as a hosted service registered <i>after</i> discovery, so its
/// <c>UPDATE</c> is the last write and wins, leaving the pt-BR label in the source of truth (the database).
/// Because everything Lumen-related is confined to the Identity module (section 8), this catalogue lives in
/// Identity Infrastructure and reuses the module permission-code constants (no magic strings).</para>
///
/// <para>An anti-drift test (<c>PermissionDisplayNameCatalogTests</c>) ties every key here 1:1 to a real
/// <c>[RequirePermission]</c> controller action, so a new gated endpoint without a pt-BR label breaks the build,
/// and an orphaned label (a renamed/removed action) also breaks it.</para>
/// </summary>
public static class PermissionDisplayNames
{
    /// <summary>
    /// Every permission code mapped to its pt-BR display name. The key is the exact Lumen code
    /// (<c>&lt;Controller&gt;.&lt;Action&gt;</c>); the value is the label the seeder writes to
    /// <c>"Lumen"."Permission"."DisplayName"</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ByCode { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Inventory — Stock (StockController)
            [InventoryPermissions.Stock.RegisterStockItem] = "Cadastrar item de estoque",
            [InventoryPermissions.Stock.RegisterEntry] = "Registrar entrada de estoque",
            [InventoryPermissions.Stock.RegisterConsumption] = "Registrar consumo",
            [InventoryPermissions.Stock.Transfer] = "Transferir estoque",
            [InventoryPermissions.Stock.Dispose] = "Descartar estoque",
            [InventoryPermissions.Stock.RegisterCount] = "Registrar contagem de estoque",

            // Inventory — Equipment (EquipmentController)
            [InventoryPermissions.Equipment.Register] = "Cadastrar equipamento",
            [InventoryPermissions.Equipment.Update] = "Editar equipamento",
            [InventoryPermissions.Equipment.ChangeStatus] = "Alterar status do equipamento",
            [InventoryPermissions.Equipment.DefineCalibration] = "Definir calibração",
            [InventoryPermissions.Equipment.RecordMaintenance] = "Registrar manutenção",

            // Inventory — Partners (PartnersController)
            [InventoryPermissions.Partners.Register] = "Cadastrar parceiro",
            [InventoryPermissions.Partners.Update] = "Editar parceiro",
            [InventoryPermissions.Partners.Deactivate] = "Desativar parceiro",
            [InventoryPermissions.Partners.Reactivate] = "Reativar parceiro",
            [InventoryPermissions.Partners.RecordSample] = "Registrar amostra",
            [InventoryPermissions.Partners.RemoveSample] = "Remover amostra",

            // Configuration (one controller per reference-data type)
            [ConfigurationPermissions.UnitCreate] = "Criar unidade de medida",
            [ConfigurationPermissions.RoomCreate] = "Criar sala",
            [ConfigurationPermissions.ReferenceRangeCreate] = "Criar faixa de referência",
            [ConfigurationPermissions.ItemCategoryCreate] = "Criar categoria de item",
            [ConfigurationPermissions.ExpiryPolicySetWarningWindow] = "Definir janela de alerta de validade",

            // Notifications (NotificationsController)
            [NotificationsPermissions.Notifications.MarkAsRead] = "Marcar notificação como lida",

            // Identity — Profiles (ProfilesController)
            [ProfilesPermissions.ListAvailablePermissions] = "Listar permissões disponíveis",
            [ProfilesPermissions.CreateProfile] = "Criar perfil",
            [ProfilesPermissions.UpdateProfile] = "Editar perfil",
            [ProfilesPermissions.SetProfilePermissions] = "Definir permissões do perfil",

            // Identity — MemberProfiles (MemberProfilesController)
            [MemberProfilesPermissions.AssignProfile] = "Atribuir perfil a membro",
            [MemberProfilesPermissions.RemoveProfile] = "Remover perfil de membro",

            // Identity — CompanyMembers (CompanyMembersController)
            [CompanyMembersPermissions.ListMembers] = "Listar membros da empresa",
            [CompanyMembersPermissions.CheckRemovalEligibility] = "Verificar elegibilidade de remoção",

            // Audit (AuditController)
            [AuditPermissions.List] = "Listar auditoria",
            [AuditPermissions.Export] = "Exportar auditoria"
        };
}
