using Microsoft.AspNetCore.Antiforgery;

namespace SISLAB.Api.Csrf;

/// <summary>
/// Registration of ASP.NET Core antiforgery for the double-submit-cookie CSRF defense
/// used by the browser SPA (which authenticates over httpOnly cookies).
///
/// <para>
/// The SPA reads the readable <c>XSRF-TOKEN</c> cookie and echoes it back in the
/// <c>X-XSRF-TOKEN</c> header on every state-changing request. A forged cross-site request
/// cannot read the cookie (same-origin policy) and therefore cannot produce a matching header,
/// so <see cref="CsrfValidationMiddleware"/> rejects it.
/// </para>
/// </summary>
public static class CsrfServiceCollectionExtensions
{
    /// <summary>Header the SPA sends the token in.</summary>
    public const string HeaderName = "X-XSRF-TOKEN";

    /// <summary>Readable (non-httpOnly) cookie the SPA reads the token from.</summary>
    public const string CookieName = "XSRF-TOKEN";

    public static IServiceCollection AddSislabAntiforgery(this IServiceCollection services)
    {
        services.AddAntiforgery(options =>
        {
            options.HeaderName = HeaderName;

            // The SPA must read this cookie via JS to echo it in the request header,
            // so it is intentionally NOT httpOnly. The value is an anti-CSRF token,
            // not a credential — the credential remains the httpOnly session cookie.
            options.Cookie.Name = CookieName;
            options.Cookie.HttpOnly = false;
            options.Cookie.SameSite = SameSiteMode.Strict;

            // HTTPS in production; allowed over HTTP in dev (matches the active-company cookie).
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        return services;
    }
}
