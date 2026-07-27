using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SISLAB.Api.Tests.Infrastructure;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> that boots the real SISLAB API for HTTP-level tests
/// (cards [E9] #58/#59) without a live PostgreSQL.
///
/// The modules register schema-migration/bootstrapper hosted services that connect to the database on
/// startup; the background jobs host does the same. For header/rate-limit/error-shape tests none of that is
/// needed, so this factory removes every <see cref="IHostedService"/> and supplies a placeholder connection
/// string, letting the pipeline (security headers, rate limiter, exception handler) be exercised in
/// isolation. The endpoints these tests hit are public (health) or fault before touching the database.
/// </summary>
public sealed class SislabApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Boot settings that <c>appsettings.json</c> deliberately does not carry usable values for, published as
    /// environment variables (<c>__</c> is the nesting separator) before any host is built.
    ///
    /// <para>They cannot be supplied through <c>ConfigureAppConfiguration</c>: under the minimal hosting model
    /// <c>Program.cs</c> reads <c>builder.Configuration</c> while still configuring services, whereas the
    /// sources registered by <see cref="WebApplicationFactory{TEntryPoint}"/> are only applied when the host is
    /// built — too late. Environment variables belong to the <c>WebApplicationBuilder</c> default sources, so
    /// they are already visible at that point.</para>
    ///
    /// <para>Both values are rejected by design outside tests, and that is intentional: the connection string
    /// ships as an empty string and the JWT secret as a 31-character placeholder, one short of the 32 the
    /// <c>IdentityJwtOptions</c> data annotation demands. A deployment that forgets to configure them fails on
    /// boot instead of running with a secret published in the repository. The suite used to survive only where
    /// User Secrets happened to hold real values, so it was green on developer machines and red on CI.</para>
    /// </summary>
    private static readonly Dictionary<string, string> BootSettings = new()
    {
        // Never opened in these tests — satisfies DbConnectionFactory / AddDbContext at build time.
        ["ConnectionStrings__SislabDb"] = "Host=localhost;Database=sislab_test;Username=u;Password=p",

        // Never used to sign anything the tests read back; only has to clear the 32-character minimum.
        ["LumenIdentity__Jwt__Secret"] = "sislab-api-tests-signing-key-not-a-real-secret"
    };

    static SislabApiFactory()
    {
        foreach (KeyValuePair<string, string> setting in BootSettings)
        {
            Environment.SetEnvironmentVariable(setting.Key, setting.Value);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Drop schema-migration/bootstrapper/job hosted services so startup does not touch PostgreSQL.
            foreach (ServiceDescriptor hostedService in services
                         .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
                         .ToList())
            {
                services.Remove(hostedService);
            }
        });
    }
}
