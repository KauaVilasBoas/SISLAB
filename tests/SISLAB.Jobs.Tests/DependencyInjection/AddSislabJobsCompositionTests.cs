using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SISLAB.Infrastructure.DependencyInjection;
using SISLAB.Infrastructure.Outbox;
using SISLAB.Jobs.Configuration;
using SISLAB.Jobs.DependencyInjection;
using SISLAB.Jobs.Jobs;
using SISLAB.Jobs.Multitenancy;
using SISLAB.Modules.Inventory.Application;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Jobs.Tests.DependencyInjection;

/// <summary>
/// Composition tests for <c>AddSislabJobs</c> (E6 #39): they prove the DI wiring of the jobs host
/// actually closes — the Outbox dispatcher example job and every collaborator it needs (the module
/// DbContext behind <see cref="IOutboxDbContext"/>, the event bus, the tenant bypass, the ambient
/// tenant context) resolve from the real host graph without a live database.
///
/// The graph is assembled exactly as <c>Program.cs</c> does: shared infrastructure, then the module,
/// then the jobs. <c>UseNpgsql</c> does not open a connection at registration, so a placeholder
/// connection string is enough to build the provider.
/// </summary>
public sealed class AddSislabJobsCompositionTests
{
    private static ServiceProvider BuildHostGraph(IConfiguration? configuration = null)
    {
        configuration ??= new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SislabDb"] = "Host=localhost;Database=sislab_test;Username=u;Password=p"
            })
            .Build();

        ServiceCollection services = new();
        services.AddLogging();

        // Mirror Program.cs composition order.
        services.AddSislabInfrastructure();
        new InventoryModule().RegisterServices(services, configuration);
        services.AddSislabJobs(configuration);

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void OutboxDispatcher_and_its_collaborators_resolve_from_a_tick_scope()
    {
        using ServiceProvider provider = BuildHostGraph();
        using IServiceScope scope = provider.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        // The example job resolves these from its per-tick scope; if any is missing the wiring is broken.
        Assert.NotNull(sp.GetRequiredService<OutboxDispatcher>());
        Assert.NotNull(sp.GetRequiredService<IOutboxDbContext>());
        Assert.NotNull(sp.GetRequiredService<ITenantBypass>());
        Assert.NotNull(sp.GetRequiredService<IEventBus>());
    }

    [Fact]
    public void The_outbox_dispatcher_job_is_registered_as_a_hosted_service()
    {
        using ServiceProvider provider = BuildHostGraph();

        IHostedService[] hostedServices = provider.GetServices<IHostedService>().ToArray();

        Assert.Contains(hostedServices, service => service is OutboxDispatcherJob);
    }

    [Fact]
    public void Ambient_tenant_context_is_available_and_settable_but_does_not_hijack_the_global_context()
    {
        using ServiceProvider provider = BuildHostGraph();
        using IServiceScope scope = provider.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        // The settable ambient context is available for the future per-company scan jobs.
        AmbientTenantContext ambient = sp.GetRequiredService<AmbientTenantContext>();
        Guid company = Guid.NewGuid();
        ambient.SetCompany(company);
        Assert.Equal(company, ambient.CompanyId);

        // Critically, AddSislabJobs must NOT rebind the global ITenantContext: in the shared API
        // container that is the request-scoped Identity TenantContext. Here Identity is not
        // registered, so resolving ITenantContext must NOT return the ambient one either — it must
        // be absent, confirming the jobs wiring never touched the global registration.
        Assert.Null(sp.GetService<ITenantContext>());
    }

    [Fact]
    public void JobsOptions_binds_intervals_from_configuration()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SislabDb"] = "Host=localhost;Database=sislab_test;Username=u;Password=p",
                ["Jobs:OutboxDispatcher:Interval"] = "00:00:30",
                ["Jobs:OutboxDispatcher:BatchSize"] = "123"
            })
            .Build();

        using ServiceProvider provider = BuildHostGraph(configuration);

        JobsOptions options = provider.GetRequiredService<IOptions<JobsOptions>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(30), options.OutboxDispatcher.Interval);
        Assert.Equal(123, options.OutboxDispatcher.BatchSize);
    }

    [Fact]
    public void JobsOptions_falls_back_to_safe_defaults_when_the_section_is_absent()
    {
        // No "Jobs" section configured — the worker must still run with sensible defaults.
        using ServiceProvider provider = BuildHostGraph();

        JobsOptions options = provider.GetRequiredService<IOptions<JobsOptions>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(5), options.OutboxDispatcher.Interval);
        Assert.Equal(50, options.OutboxDispatcher.BatchSize);
    }
}
