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
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Never opened in these tests — satisfies DbConnectionFactory / AddDbContext at build time.
                ["ConnectionStrings:SislabDb"] = "Host=localhost;Database=sislab_test;Username=u;Password=p"
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
