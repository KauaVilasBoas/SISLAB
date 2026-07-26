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
    // Never opened in these tests — satisfies DbConnectionFactory / AddDbContext at build time.
    private const string PlaceholderConnectionString =
        "Host=localhost;Database=sislab_test;Username=u;Password=p";

    /// <summary>
    /// Publishes the placeholder connection string as an environment variable before any host is built.
    ///
    /// <para>It cannot be supplied through <c>ConfigureAppConfiguration</c>: under the minimal hosting model
    /// <c>Program.cs</c> reads <c>builder.Configuration</c> while still configuring services (the
    /// <c>ModuleLoader.RegisterModules</c> call), whereas the sources registered by
    /// <see cref="WebApplicationFactory{TEntryPoint}"/> are only applied when the host is built — too late.
    /// Environment variables are part of the <c>WebApplicationBuilder</c> default sources, so they are already
    /// visible at that point.</para>
    ///
    /// <para>Without this, the boot only survived where User Secrets happened to hold a real
    /// <c>ConnectionStrings:SislabDb</c>: <c>appsettings.json</c> ships the key as an empty string, which slips
    /// past the <c>?? throw</c> guard in <c>AddIdentityModule</c> and fails deeper in, inside
    /// <c>AddLumenAuthorization</c>. Tests passed on developer machines and failed on CI.</para>
    /// </summary>
    static SislabApiFactory() =>
        Environment.SetEnvironmentVariable("ConnectionStrings__SislabDb", PlaceholderConnectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SislabDb"] = PlaceholderConnectionString
            }));

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
