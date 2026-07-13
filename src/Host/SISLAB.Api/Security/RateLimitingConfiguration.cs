using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SISLAB.SharedKernel.Http;

namespace SISLAB.Api.Security;

/// <summary>
/// Per-IP rate limiting (card [E9] #58) using the built-in .NET 8 <see cref="RateLimiter"/>.
///
/// <list type="bullet">
///   <item><b>login</b> — the authentication surface (<c>/api/auth/*</c>): 10 requests/minute per IP, to
///   blunt credential-stuffing and CSRF-token farming.</item>
///   <item><b>api</b> — everything else: 300 requests/minute per IP, a generous ceiling that still caps a
///   runaway or abusive client.</item>
/// </list>
///
/// The policy is chosen by a single global limiter keyed on the client IP and the path prefix, so it
/// applies uniformly without per-endpoint attributes — important because the auth endpoints are mapped by
/// Lumen, which the Host cannot decorate. A rejected request gets <c>429</c> with the uniform
/// <see cref="ApiResult"/> error envelope.
/// </summary>
internal static class RateLimitingConfiguration
{
    private const string AuthPathPrefix = "/api/auth";

    private const int LoginPermitPerMinute = 10;
    private const int ApiPermitPerMinute = 300;

    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(BuildPartition);

        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(
                new ApiResult(false, "Too many requests."), cancellationToken);
        };
    }

    private static RateLimitPartition<string> BuildPartition(HttpContext httpContext)
    {
        string clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        bool isAuthRequest = httpContext.Request.Path
            .StartsWithSegments(AuthPathPrefix, StringComparison.OrdinalIgnoreCase);

        // Partition by policy + IP so the two ceilings are tracked independently per client.
        (string policy, int permitPerMinute) = isAuthRequest
            ? (HttpConstants.RateLimitPolicies.Login, LoginPermitPerMinute)
            : (HttpConstants.RateLimitPolicies.Api, ApiPermitPerMinute);

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{policy}:{clientIp}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    }
}
