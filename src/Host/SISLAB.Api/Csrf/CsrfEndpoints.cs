using Microsoft.AspNetCore.Antiforgery;

namespace SISLAB.Api.Csrf;

/// <summary>
/// Public endpoint that issues the CSRF token cookie for browser clients.
/// The SPA calls this once (typically on app bootstrap and after login) to obtain the
/// readable <c>XSRF-TOKEN</c> cookie, then echoes it in the <c>X-XSRF-TOKEN</c> header
/// on every state-changing request.
/// </summary>
internal static class CsrfEndpoints
{
    public static IEndpointRouteBuilder MapCsrfEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Public: reachable before authentication so the SPA can arm CSRF protection up front.
        // GET is a safe method, so CsrfValidationMiddleware never validates this call itself.
        endpoints
            .MapGet("/api/auth/csrf", IssueToken)
            .WithTags("Auth")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent);

        return endpoints;
    }

    private static IResult IssueToken(HttpContext context, IAntiforgery antiforgery)
    {
        // Generates the request token and writes the readable XSRF-TOKEN cookie via Set-Cookie.
        antiforgery.GetAndStoreTokens(context);
        return Results.NoContent();
    }
}
