namespace SISLAB.SharedKernel.Http;

/// <summary>
/// Canonical HTTP-layer literals shared across the Host and modules (card [E9] #58/#59): response content
/// types, the per-IP rate-limit policy names and the baseline security response headers. Centralized so the
/// wire contract lives in one place and the middleware, configuration and tests cannot drift apart.
/// </summary>
public static class HttpConstants
{
    /// <summary>Response content types SISLAB emits directly (outside MVC's negotiated formatters).</summary>
    public static class ContentTypes
    {
        /// <summary>RFC 7807 problem document media type used by the error boundary.</summary>
        public const string ProblemJson = "application/problem+json";

        /// <summary>CSV media type used by data exports (e.g. the audit trail export).</summary>
        public const string Csv = "text/csv";
    }

    /// <summary>Names of the per-IP rate-limit partitions (card [E9] #58).</summary>
    public static class RateLimitPolicies
    {
        /// <summary>Strict window guarding the authentication surface (<c>/api/auth/*</c>).</summary>
        public const string Login = "login";

        /// <summary>Generous window for every other endpoint.</summary>
        public const string Api = "api";
    }

    /// <summary>
    /// Baseline security response headers (card [E9] #58): defense-in-depth browser hardening applied to
    /// every response by the Host's <c>SecurityHeadersMiddleware</c>.
    /// </summary>
    public static class SecurityHeaders
    {
        public const string ContentTypeOptions = "X-Content-Type-Options";
        public const string ContentTypeOptionsValue = "nosniff";

        public const string FrameOptions = "X-Frame-Options";
        public const string FrameOptionsValue = "DENY";

        public const string ReferrerPolicy = "Referrer-Policy";
        public const string ReferrerPolicyValue = "strict-origin-when-cross-origin";

        public const string PermissionsPolicy = "Permissions-Policy";
        public const string PermissionsPolicyValue = "camera=(), microphone=(), geolocation=()";

        public const string XssProtection = "X-XSS-Protection";
        public const string XssProtectionValue = "0";
    }
}
