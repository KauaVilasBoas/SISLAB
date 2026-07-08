namespace SISLAB.Modules.Identity.Contracts.Authorization;

/// <summary>
/// Catálogo de <b>permission codes</b> do bounded context Identity, consumidos pela
/// autorização granular da Lumen (<c>[RequirePermission(code)]</c>).
///
/// <para><b>Convenção de code (SISLAB):</b> <c>&lt;recurso-no-plural&gt;.&lt;ação&gt;</c>,
/// tudo em <i>lowercase</i> e separado por ponto. O recurso é o agregado/módulo alvo
/// (ex.: <c>companies</c>, <c>items</c>, <c>movements</c>); a ação é o verbo de negócio
/// (<c>read</c>, <c>manage</c>, <c>create</c>, <c>delete</c>, ...). Prefira <c>manage</c>
/// para a operação de escrita ampla quando não houver necessidade de granularidade fina.</para>
///
/// <para>Esses codes são <b>descobertos no boot</b> pela discovery da Lumen (que varre os
/// controllers MVC decorados com <c>[RequirePermission]</c>), materializados como
/// <c>Permission</c> e reconciliados no profile <c>Administrator</c>. Manter os codes aqui,
/// centralizados e tipados, evita <i>magic strings</i> espalhadas e permite reuso em testes.</para>
/// </summary>
public static class IdentityPermissions
{
    /// <summary>Recurso de gestão de empresas (tenants) e sua composição de membros.</summary>
    public static class Companies
    {
        /// <summary>Leitura de dados da company ativa (ex.: listar membros). Escopo: company ativa.</summary>
        public const string Read = "companies.read";

        /// <summary>Gestão da company ativa e de seus membros (escrita). Escopo: company ativa.</summary>
        public const string Manage = "companies.manage";
    }
}
