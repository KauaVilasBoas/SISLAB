using Microsoft.AspNetCore.Http;

namespace SISLAB.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// Single source of truth for the browser session cookies that carry the Lumen-issued
/// tokens (card [E7] #44). The decision is fixed by the product owner: the SPA authenticates
/// over <b>httpOnly cookies</b> — never localStorage, never a Bearer header in the browser.
///
/// <para>Two cookies, both httpOnly so JavaScript cannot read them (XSS defense):</para>
/// <list type="bullet">
///   <item><see cref="AccessTokenName"/> — the short-lived JWT. Read back by
///     <c>JwtBearerEvents.OnMessageReceived</c> so the existing <c>UseAuthentication</c> pipeline
///     authenticates the browser exactly as if it were a Bearer request.</item>
///   <item><see cref="RefreshTokenName"/> — the long-lived refresh token, scoped to the refresh
///     endpoint path so it is not sent on every request. Consumed by <c>POST /api/auth/refresh</c>.</item>
/// </list>
///
/// <para>Centralizing the flags here keeps the bridge that writes them, the JwtBearer hook that reads
/// the access cookie, and the logout that clears them from ever diverging — the same rationale as
/// <c>ActiveCompanyCookie</c>.</para>
/// </summary>
public static class SessionCookies
{
    /// <summary>httpOnly cookie holding the Lumen access token (JWT). Read by the JwtBearer hook.</summary>
    public const string AccessTokenName = "sislab_access_token";

    /// <summary>httpOnly cookie holding the Lumen refresh token. Sent only to the refresh endpoint.</summary>
    public const string RefreshTokenName = "sislab_refresh_token";

    /// <summary>Path the refresh cookie is scoped to — it is only ever needed by the refresh endpoint.</summary>
    public const string RefreshTokenPath = "/api/auth/refresh";

    /// <summary>
    /// Writes both session cookies on a successful login/refresh.
    ///
    /// <para>Flags: HttpOnly (JS cannot read the credential), SameSite=Lax (safe default for the
    /// dev same-origin Vite proxy; the SPA is served same-site so Lax carries the cookie on
    /// top-level navigations and same-site XHR), Secure when the request is HTTPS, IsEssential
    /// (not gated by cookie-consent). The access cookie lives at <c>/</c> so it rides every API call;
    /// the refresh cookie is pinned to <see cref="RefreshTokenPath"/>.</para>
    ///
    /// <para><paramref name="accessMaxAge"/> mirrors the JWT lifetime so the cookie expires with the
    /// token. The refresh cookie gets a longer, fixed window (<paramref name="refreshMaxAge"/>).</para>
    /// </summary>
    public static void Write(
        HttpResponse response,
        string accessToken,
        string refreshToken,
        bool isSecure,
        TimeSpan accessMaxAge,
        TimeSpan refreshMaxAge)
    {
        response.Cookies.Append(
            AccessTokenName,
            accessToken,
            BuildOptions(isSecure, path: "/", maxAge: accessMaxAge));

        response.Cookies.Append(
            RefreshTokenName,
            refreshToken,
            BuildOptions(isSecure, path: RefreshTokenPath, maxAge: refreshMaxAge));
    }

    /// <summary>
    /// Clears both session cookies on logout. Deletion must repeat the exact Path each cookie was
    /// written with, otherwise the browser keeps the original cookie.
    /// </summary>
    public static void Clear(HttpResponse response, bool isSecure)
    {
        response.Cookies.Delete(AccessTokenName, BuildDeleteOptions(isSecure, path: "/"));
        response.Cookies.Delete(RefreshTokenName, BuildDeleteOptions(isSecure, path: RefreshTokenPath));
    }

    private static CookieOptions BuildOptions(bool isSecure, string path, TimeSpan maxAge) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = isSecure,
        Path = path,
        MaxAge = maxAge,
        IsEssential = true
    };

    private static CookieOptions BuildDeleteOptions(bool isSecure, string path) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = isSecure,
        Path = path,
        IsEssential = true
    };
}
