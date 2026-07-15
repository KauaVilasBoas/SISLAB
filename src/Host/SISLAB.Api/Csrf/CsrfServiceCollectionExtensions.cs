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
            // The ASP.NET Core session cookie keeps its auto-generated name (httpOnly, hidden).
            // The readable XSRF-TOKEN cookie is written manually in CsrfEndpoints with the
            // REQUEST token so the SPA can echo it in X-XSRF-TOKEN — not the session token.
        });

        return services;
    }
}
