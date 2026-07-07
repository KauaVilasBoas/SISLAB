using Microsoft.AspNetCore.Http;

namespace SISLAB.Modules.Identity.Infrastructure.Multitenancy;

/// <summary>
/// Convenção única do cookie de company ativa (Opção A: companyId FORA do JWT,
/// mantido em cookie httpOnly + SameSite). Centralizar aqui evita divergência entre
/// o middleware que lê o cookie e os endpoints que o escrevem.
/// </summary>
internal static class ActiveCompanyCookie
{
    /// <summary>Nome do cookie que carrega o ID da company ativa.</summary>
    public const string Name = "sislab_active_company";

    /// <summary>
    /// Opções padrão do cookie de company ativa.
    ///
    /// - HttpOnly: o SPA nunca lê o valor via JS (defesa contra XSS).
    /// - SameSite=Lax: default sensato para dev same-site; endurecer para None+Secure
    ///   quando o SPA for servido em origem distinta (E7). Documentado no DEV_SETUP.
    /// - Secure: acompanha a request (HTTPS em produção; permitido em http de dev).
    /// - Path "/": disponível para toda a API.
    /// </summary>
    public static CookieOptions BuildOptions(bool isSecure) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = isSecure,
        Path = "/",
        IsEssential = true
    };
}
