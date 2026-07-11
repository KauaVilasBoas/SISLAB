using Microsoft.AspNetCore.Antiforgery;
using SISLAB.Api.Csrf;
using SISLAB.SharedKernel.Http;

namespace SISLAB.Api.Middleware;

/// <summary>
/// Enforces CSRF protection for the cookie-authenticated browser flow.
///
/// <para>Runs AFTER <c>UseAuthentication</c> and BEFORE <c>UseAuthorization</c>.</para>
///
/// <para>A request is validated only when ALL of the following hold:</para>
/// <list type="bullet">
///   <item>the method is unsafe (POST/PUT/PATCH/DELETE);</item>
///   <item>the path is not exempt (see <see cref="CsrfPolicy"/>);</item>
///   <item>the request actually carries the <c>XSRF-TOKEN</c> cookie — i.e. it is a browser
///     session that armed CSRF protection. Pure-Bearer, non-browser clients never receive
///     that cookie and are therefore not subject to CSRF (their credential is not ambient).</item>
/// </list>
///
/// <para>
/// When validation fails, the request is short-circuited with 403 and the uniform
/// <see cref="ApiResult"/> error envelope — consistent with <see cref="ExceptionHandlingMiddleware"/>.
/// </para>
/// </summary>
public sealed class CsrfValidationMiddleware
{
    private const string FailureMessage = "CSRF token validation failed.";

    private readonly RequestDelegate _next;
    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<CsrfValidationMiddleware> _logger;

    public CsrfValidationMiddleware(
        RequestDelegate next,
        IAntiforgery antiforgery,
        ILogger<CsrfValidationMiddleware> logger)
    {
        _next = next;
        _antiforgery = antiforgery;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresValidation(context))
        {
            try
            {
                await _antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException ex)
            {
                _logger.LogWarning(ex,
                    "CSRF validation failed for {Method} {Path}.",
                    context.Request.Method, context.Request.Path);

                await WriteForbiddenAsync(context);
                return;
            }
        }

        await _next(context);
    }

    private static bool RequiresValidation(HttpContext context)
    {
        HttpRequest request = context.Request;

        if (CsrfPolicy.IsSafeMethod(request.Method))
            return false;

        if (CsrfPolicy.IsExemptPath(request.Path))
            return false;

        // No CSRF cookie => not a browser session that armed CSRF protection.
        // This is a pure-Bearer (non-browser) client whose credential is not sent
        // ambiently by the browser, so it cannot be a CSRF victim.
        return request.Cookies.ContainsKey(CsrfServiceCollectionExtensions.CookieName);
    }

    private static Task WriteForbiddenAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new ApiResult(false, FailureMessage));
    }
}
