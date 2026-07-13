using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SISLAB.Infrastructure.Messaging;
using SISLAB.Infrastructure.Outbox;
using SISLAB.Jobs.Configuration;
using SISLAB.Jobs.Jobs;
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

        // 4. Tenant-override seam for the per-company alert scans (Fork #1 → A). It is contributed by
        //    AddSislabInfrastructure (ITenantContextOverride → TenantContextOverride, Scoped) and read by
        //    the effective ITenantContext (OverridableTenantContext, composed in the Identity module). This
        //    host adds NO ITenantContext registration of its own: on the HTTP path the request tenant is
        //    intact, and in a job the alert base sets this override per company inside a bypass scope. The
        //    settable seam therefore needs no wiring here beyond what the shared infrastructure already did.

        // 5. The scheduled jobs. Registered as IHostedService so the host's generic-host lifetime
        //    starts/stops them.
        //    - OutboxDispatcherJob: the E6 #39 example job (and the core of #40).
        //    - The three alert jobs (#41/#42/#66): each a CompanyScanAlertJob that enumerates the active
        //      companies (ListAllCompanyIdsQuery) under a tenant bypass and, per company, runs its E4/E6
        //      read query behind the tenant-override seam and raises notifications via INotificationPublisher.
        services.AddHostedService<OutboxDispatcherJob>();
        services.AddHostedService<ExpiryAlertJob>();
        services.AddHostedService<LowStockAlertJob>();
        services.AddHostedService<CalibrationOverdueAlertJob>();

        return services;
    }
}
