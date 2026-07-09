using Microsoft.AspNetCore.Http;

namespace SISLAB.Modules.Identity.Infrastructure.Multitenancy;

/// <summary>
/// Single source of truth for the active-company cookie convention (companyId outside the JWT,
/// stored in an httpOnly + SameSite cookie). Centralizing here prevents divergence between the
/// middleware that reads the cookie and the endpoints that write it.
/// </summary>
internal static class ActiveCompanyCookie
{
    public const string Name = "sislab_active_company";

    /// <summary>
    /// Default cookie options for the active company.
    ///
    /// - HttpOnly: the SPA cannot read the value via JS (XSS defense).
    /// - SameSite=Lax: safe default for dev same-site; tighten to None+Secure
    ///   when the React SPA runs on a different origin (E7). Documented in DEV_SETUP.md.
    /// - Secure: follows the request (HTTPS in production; allowed over HTTP in dev).
    /// - Path "/": available to the entire API.
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
