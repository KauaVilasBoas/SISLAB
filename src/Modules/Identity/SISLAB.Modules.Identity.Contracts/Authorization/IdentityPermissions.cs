namespace SISLAB.Modules.Identity.Contracts.Authorization;

/// <summary>
/// Catálogo de <b>permission codes</b> do bounded context Identity, consumidos pela
/// autorização granular da Lumen (<c>[RequirePermission]</c> nos controllers admin).
///
/// <para><b>Convenção de code (imposta pela Lumen 1.1.0):</b> o code é sempre
/// <c>&lt;Controller&gt;.&lt;Action&gt;</c>, onde <c>Controller</c> é o nome da classe do controller
/// sem o sufixo <c>Controller</c> e <c>Action</c> é o nome do método da ação (ambos em PascalCase,
/// exatamente como no C#). Isso <b>não é uma escolha do SISLAB</b>: o <c>Permission.Create</c> da
/// Lumen recomputa o code a partir de controller+action e ignora qualquer string passada ao
/// atributo. Por isso decoramos com <c>[RequirePermission]</c> <i>sem</i> code explícito — a
/// discovery grava <c>Controller.Action</c> e o enforcement deriva o mesmo, mantendo-os em sincronia.</para>
///
/// <para>Manter os codes aqui, centralizados e tipados, evita <i>magic strings</i> nos testes e
/// consumidores. Se um método for renomeado, seu code muda — atualize a constante correspondente.</para>
/// </summary>
public static class IdentityPermissions
{
    /// <summary>
    /// Permissões do controller de administração de membros da company ativa
    /// (<c>CompanyMembersController</c> → prefixo <c>CompanyMembers</c>).
    /// </summary>
    public static class CompanyMembers
    {
        /// <summary>Listar membros da company ativa (ação <c>ListMembers</c>). Escopo: company ativa.</summary>
        public const string ListMembers = "CompanyMembers.ListMembers";

        /// <summary>
        /// Verificar elegibilidade de remoção de membro (ação <c>CheckRemovalEligibility</c>) —
        /// representa a permissão de gestão/escrita. Escopo: company ativa.
        /// </summary>
        public const string CheckRemovalEligibility = "CompanyMembers.CheckRemovalEligibility";
    }
}
