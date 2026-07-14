using SISLAB.SharedKernel.Authorization;

namespace SISLAB.Modules.Identity.Infrastructure.Authorization;

/// <summary>
/// SISLAB's authoritative permission catalogue: the groups and permissions
/// <see cref="LumenPermissionCatalogSeeder"/> seeds into <c>"Lumen"."PermissionGroup"</c> and
/// <c>"Lumen"."Permission"</c>. Since Lumen.Authorization 2.0.0 inverted catalogue ownership (the library
/// only validates, no longer writes), this is the single source of truth for every permission the product
/// exposes — one entry per <c>[RequirePermission]</c>-gated action plus a few catalogued read codes.
///
/// <para><b>No magic strings.</b> Every <see cref="Permission.Code"/> comes from the SharedKernel permission
/// constants (<see cref="InventoryPermissions"/>, <see cref="ConfigurationPermissions"/>, …), so renaming or
/// removing a constant breaks compilation here. The pt-BR <see cref="Permission.DisplayName"/> is the label the
/// profile-management UI shows on the permission checkboxes.</para>
///
/// <para><b>Deterministic identity.</b> Group and permission ids are fixed UUID literals so the seeder's
/// <c>ON CONFLICT ("Id") DO NOTHING</c> is stable across restarts and environments.</para>
/// </summary>
public static class PermissionCatalog
{
    /// <summary>A permission group (checkbox section in the profile UI).</summary>
    public sealed record Group(Guid Id, string Name, string Description, IReadOnlyList<Permission> Permissions);

    /// <summary>A single permission (one gated action) with its pt-BR label.</summary>
    public sealed record Permission(Guid Id, string Code, string DisplayName);

    /// <summary>Every group with its permissions, in display order.</summary>
    public static IReadOnlyList<Group> Groups { get; } = new List<Group>
    {
        new(
            Id: new Guid("b1a10000-0000-4000-a000-000000000001"),
            Name: "Estoque",
            Description: "Permissões do módulo de estoque (itens, equipamentos e parceiros)",
            Permissions: new List<Permission>
            {
                // Stock (StockController)
                new(new Guid("b1a11000-0000-4000-a000-000000000001"), InventoryPermissions.Stock.RegisterStockItem, "Cadastrar item de estoque"),
                new(new Guid("b1a11000-0000-4000-a000-000000000002"), InventoryPermissions.Stock.RegisterEntry, "Registrar entrada de estoque"),
                new(new Guid("b1a11000-0000-4000-a000-000000000003"), InventoryPermissions.Stock.RegisterConsumption, "Registrar consumo"),
                new(new Guid("b1a11000-0000-4000-a000-000000000004"), InventoryPermissions.Stock.Transfer, "Transferir estoque"),
                new(new Guid("b1a11000-0000-4000-a000-000000000005"), InventoryPermissions.Stock.Dispose, "Descartar estoque"),
                new(new Guid("b1a11000-0000-4000-a000-000000000006"), InventoryPermissions.Stock.RegisterCount, "Registrar contagem de estoque"),

                // Equipment (EquipmentController)
                new(new Guid("b1a12000-0000-4000-a000-000000000001"), InventoryPermissions.Equipment.Register, "Cadastrar equipamento"),
                new(new Guid("b1a12000-0000-4000-a000-000000000002"), InventoryPermissions.Equipment.Update, "Editar equipamento"),
                new(new Guid("b1a12000-0000-4000-a000-000000000003"), InventoryPermissions.Equipment.ChangeStatus, "Alterar status do equipamento"),
                new(new Guid("b1a12000-0000-4000-a000-000000000004"), InventoryPermissions.Equipment.DefineCalibration, "Definir calibração"),
                new(new Guid("b1a12000-0000-4000-a000-000000000005"), InventoryPermissions.Equipment.RecordMaintenance, "Registrar manutenção"),

                // Partners (PartnersController)
                new(new Guid("b1a13000-0000-4000-a000-000000000001"), InventoryPermissions.Partners.Register, "Cadastrar parceiro"),
                new(new Guid("b1a13000-0000-4000-a000-000000000002"), InventoryPermissions.Partners.Update, "Editar parceiro"),
                new(new Guid("b1a13000-0000-4000-a000-000000000003"), InventoryPermissions.Partners.Deactivate, "Desativar parceiro"),
                new(new Guid("b1a13000-0000-4000-a000-000000000004"), InventoryPermissions.Partners.Reactivate, "Reativar parceiro"),
                new(new Guid("b1a13000-0000-4000-a000-000000000005"), InventoryPermissions.Partners.RecordSample, "Registrar amostra"),
                new(new Guid("b1a13000-0000-4000-a000-000000000006"), InventoryPermissions.Partners.RemoveSample, "Remover amostra")
            }),

        new(
            Id: new Guid("b1a20000-0000-4000-a000-000000000001"),
            Name: "Configuração",
            Description: "Permissões de dados de referência da empresa (unidades, salas, categorias, políticas de validade)",
            Permissions: new List<Permission>
            {
                new(new Guid("b1a21000-0000-4000-a000-000000000001"), ConfigurationPermissions.UnitCreate, "Criar unidade de medida"),
                new(new Guid("b1a21000-0000-4000-a000-000000000002"), ConfigurationPermissions.RoomCreate, "Criar sala"),
                new(new Guid("b1a21000-0000-4000-a000-000000000003"), ConfigurationPermissions.ReferenceRangeCreate, "Criar faixa de referência"),
                new(new Guid("b1a21000-0000-4000-a000-000000000004"), ConfigurationPermissions.ItemCategoryCreate, "Criar categoria de item"),
                new(new Guid("b1a21000-0000-4000-a000-000000000005"), ConfigurationPermissions.ExpiryPolicySetWarningWindow, "Definir janela de alerta de validade")
            }),

        new(
            Id: new Guid("b1a30000-0000-4000-a000-000000000001"),
            Name: "Notificações",
            Description: "Permissões de gestão de notificações e alertas",
            Permissions: new List<Permission>
            {
                new(new Guid("b1a31000-0000-4000-a000-000000000001"), NotificationsPermissions.Notifications.MarkAsRead, "Marcar notificação como lida")
            }),

        new(
            Id: new Guid("b1a40000-0000-4000-a000-000000000001"),
            Name: "Perfis e Permissões",
            Description: "Permissões de gestão de perfis de autorização e sua atribuição a membros",
            Permissions: new List<Permission>
            {
                // Profiles (ProfilesController)
                new(new Guid("b1a41000-0000-4000-a000-000000000001"), ProfilesPermissions.ListAvailablePermissions, "Listar permissões disponíveis"),
                new(new Guid("b1a41000-0000-4000-a000-000000000002"), ProfilesPermissions.CreateProfile, "Criar perfil"),
                new(new Guid("b1a41000-0000-4000-a000-000000000003"), ProfilesPermissions.UpdateProfile, "Editar perfil"),
                new(new Guid("b1a41000-0000-4000-a000-000000000004"), ProfilesPermissions.SetProfilePermissions, "Definir permissões do perfil"),

                // MemberProfiles (MemberProfilesController)
                new(new Guid("b1a42000-0000-4000-a000-000000000001"), MemberProfilesPermissions.AssignProfile, "Atribuir perfil a membro"),
                new(new Guid("b1a42000-0000-4000-a000-000000000002"), MemberProfilesPermissions.RemoveProfile, "Remover perfil de membro")
            }),

        new(
            Id: new Guid("b1a50000-0000-4000-a000-000000000001"),
            Name: "Membros da Empresa",
            Description: "Permissões de administração de membros da empresa",
            Permissions: new List<Permission>
            {
                new(new Guid("b1a51000-0000-4000-a000-000000000001"), CompanyMembersPermissions.ListMembers, "Listar membros da empresa"),
                new(new Guid("b1a51000-0000-4000-a000-000000000002"), CompanyMembersPermissions.CheckRemovalEligibility, "Verificar elegibilidade de remoção")
            }),

        new(
            Id: new Guid("b1a60000-0000-4000-a000-000000000001"),
            Name: "Auditoria",
            Description: "Permissões de consulta e exportação da trilha de auditoria",
            Permissions: new List<Permission>
            {
                new(new Guid("b1a61000-0000-4000-a000-000000000001"), AuditPermissions.List, "Listar auditoria"),
                new(new Guid("b1a61000-0000-4000-a000-000000000002"), AuditPermissions.Export, "Exportar auditoria")
            })
    };
}
