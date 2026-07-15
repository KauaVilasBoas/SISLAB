using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SISLAB.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// Teaches Lumen's already-registered JWT Bearer scheme to read the access token from the
/// <see cref="SessionCookies.AccessTokenName"/> httpOnly cookie (card [E7] #44).
///
/// <para>Lumen's <c>AddLumenIdentityAuthentication</c> configures <c>AddJwtBearer</c> with the token
/// validation parameters but only sets <c>JwtBearerEvents</c> for a SignalR hub path — for a plain HTTP
/// host it leaves <c>Options.Events</c> null, so the default extraction reads solely the
/// <c>Authorization: Bearer</c> header. The SPA never sends that header; its credential is the httpOnly
/// cookie. A <see cref="IPostConfigureOptions{TOptions}"/> registered AFTER <c>AddLumenIdentity</c> layers an
/// <c>OnMessageReceived</c> hook onto the same named options, so the existing <c>UseAuthentication</c>
/// middleware authenticates the browser from the cookie with zero pipeline changes.</para>
///
/// <para>The header still wins: when an <c>Authorization</c> header is present the cookie is ignored, so
/// pure-Bearer/non-browser clients (and Swagger) keep working unchanged.</para>
/// </summary>
public static class JwtBearerCookieAuthenticationExtensions
{
    public static IServiceCollection AddJwtBearerCookieExtraction(this IServiceCollection services)
    {
        // PostConfigure targets the JwtBearer scheme's named options and runs after Lumen's AddJwtBearer
        // configuration, layering the cookie hook without replacing the validation parameters.
        services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, JwtBearerCookiePostConfigure>();
        return services;
    }

    private sealed class JwtBearerCookiePostConfigure : IPostConfigureOptions<JwtBearerOptions>
    {
        public void PostConfigure(string? name, JwtBearerOptions options)
        {
            if (name != JwtBearerDefaults.AuthenticationScheme)
                return;

            JwtBearerEvents events = options.Events ??= new JwtBearerEvents();

            // Preserve any hook Lumen may have installed, then fall back to the cookie.
            Func<MessageReceivedContext, Task> previous = events.OnMessageReceived;
            events.OnMessageReceived = async context =>
            {
                if (previous is not null)
                    await previous(context);

                // Only source the token from the cookie when the middleware hasn't already resolved one
                // (e.g. from a previous hook) and no Authorization header was supplied — the header wins.
                bool hasAuthorizationHeader =
                    context.Request.Headers.ContainsKey("Authorization");

                if (string.IsNullOrEmpty(context.Token) && !hasAuthorizationHeader)
                {
                    string? cookieToken = context.Request.Cookies[SessionCookies.AccessTokenName];
                    if (!string.IsNullOrEmpty(cookieToken))
                        context.Token = cookieToken;
                }
            };
        }
    }
}
