namespace SISLAB.Api.Csrf;

/// <summary>
/// Single source of truth for which requests require CSRF validation.
///
/// <para>
/// CSRF only threatens requests the browser sends automatically with ambient credentials
/// (the httpOnly session cookie). Two categories are therefore exempt:
/// </para>
/// <list type="bullet">
///   <item>
///     Safe HTTP methods (GET/HEAD/OPTIONS/TRACE) — read-only, never mutate state.
///   </item>
///   <item>
///     Public auth/infra routes reached BEFORE a session exists (login, refresh, register,
///     password reset) plus health and swagger. There is no session cookie to ride on yet,
///     so there is nothing to forge.
///   </item>
/// </list>
/// </summary>
internal static class CsrfPolicy
{
    private static readonly string[] ExemptPathPrefixes =
    [
        // Auth endpoints run before a session cookie is issued (login/refresh/register/reset).
        "/api/auth",
        "/health",
        "/swagger"
    ];

    public static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method)
        || HttpMethods.IsHead(method)
        || HttpMethods.IsOptions(method)
        || HttpMethods.IsTrace(method);

    public static bool IsExemptPath(PathString path)
    {
        foreach (string prefix in ExemptPathPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
