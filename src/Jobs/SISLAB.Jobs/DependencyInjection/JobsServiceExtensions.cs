using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SISLAB.Infrastructure.Messaging;
using SISLAB.Infrastructure.Outbox;
using SISLAB.Jobs.Configuration;
using SISLAB.Jobs.Jobs;
using SISLAB.Jobs.Multitenancy;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Jobs.DependencyInjection;

/// <summary>
/// Composition of the SISLAB background jobs (E6 #39).
///
/// <c>SISLAB.Jobs</c> is a library, not a process: the host (currently <c>SISLAB.Api</c>) calls
/// <see cref="AddSislabJobs"/> to register the scheduled <see cref="Microsoft.Extensions.Hosting.IHostedService"/>s
/// and their supporting services into its own container. The jobs then share the API's DI graph —
/// the same module DbContexts, the same Outbox, the same <c>IClock</c> — with no second composition
/// root (Fork #1 → C).
///
/// This method must be called AFTER the modules are registered (they contribute
/// <c>IOutboxDbContext</c> and the write-side DbContexts the Outbox dispatcher depends on).
/// </summary>
public static class JobsServiceExtensions
{
    public static IServiceCollection AddSislabJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Bind per-job configuration (intervals, batch sizes) from the "Jobs" section. Absent
        //    keys fall back to JobsOptions' defaults, so the worker runs with zero configuration.
        services
            .AddOptions<JobsOptions>()
            .Bind(configuration.GetSection(JobsOptions.SectionName));

        // 2. Integration event bus (SISLAB's IEventBus). The OutboxDispatcher publishes through it,
        //    and the in-process InMemoryEventBus fans out to the module IIntegrationEventHandler<T>s
        //    (e.g. the Inventory read-model projection). This is the first host to actually RUN the
        //    dispatcher, so the binding lives here. TryAdd keeps it idempotent if a future host (or a
        //    broker-backed bus) registers one first.
        services.TryAddSingleton<IEventBus, InMemoryEventBus>();

        // 3. The Outbox dispatcher. Scoped because it depends on the scoped IOutboxDbContext (a module
        //    DbContext); the job resolves it from its per-tick scope. Registered here (not in a module)
        //    because it is host/background infrastructure, not a module concern.
        services.TryAddScoped<OutboxDispatcher>();

        // 4. Settable ambient tenant context for the jobs host (Fork #2 → A). Registered as its own
        //    concrete type ONLY — it deliberately does NOT rebind the global ITenantContext, which in
        //    the shared API container is the request-scoped Identity TenantContext populated by the
        //    tenant-resolution middleware. Rebinding it globally would make every HTTP request resolve
        //    this empty ambient context instead, breaking request tenant isolation. The future
        //    per-company alert jobs (#41/#42/#66) set the company on this instance inside a tenant
        //    bypass scope; wiring the E4 read queries to resolve it within a tick scope is their call.
        services.TryAddScoped<AmbientTenantContext>();

        // 5. The scheduled jobs. Registered as IHostedService so the host's generic-host lifetime
        //    starts/stops them. OutboxDispatcherJob is the E6 #39 example job (and the core of #40).
        services.AddHostedService<OutboxDispatcherJob>();

        return services;
    }
}
