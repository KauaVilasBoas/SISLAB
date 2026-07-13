using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SISLAB.Infrastructure.Outbox;
using SISLAB.Jobs.Configuration;
using SISLAB.Jobs.Scheduling;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Jobs.Jobs;

/// <summary>
/// The E6 #39 example job: a scheduled worker that drains the Transactional Outbox.
///
/// On each tick it opens an auditable <see cref="ITenantBypass"/> scope (cross-tenant system work —
/// the Outbox spans every company) and delegates to the existing
/// <see cref="OutboxDispatcher.ProcessPendingAsync"/>, which publishes pending integration events via
/// <c>IEventBus</c> and marks them processed. All scheduling, per-tick DI scoping and error handling
/// come from <see cref="TimedBackgroundService"/>; this class only expresses the job's intent.
///
/// <para>
/// This also fulfils the core of card #40 (Outbox processor). It is delivered here as #39's example
/// job; the overlap is intentional and flagged for #40 to formalise.
/// </para>
/// </summary>
public sealed class OutboxDispatcherJob : TimedBackgroundService
{
    private const string BypassReason = "outbox-dispatch";

    private readonly OutboxDispatcherOptions _options;

    public OutboxDispatcherJob(
        IServiceScopeFactory scopeFactory,
        IOptions<JobsOptions> options,
        ILogger<OutboxDispatcherJob> logger)
        : base(scopeFactory, logger)
    {
        _options = options.Value.OutboxDispatcher;
    }

    /// <inheritdoc />
    protected override TimeSpan Interval => _options.Interval;

    /// <inheritdoc />
    protected override async Task ExecuteTickAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        // Scoped collaborators resolved from THIS tick's scope (the dispatcher and the module
        // DbContext behind IOutboxDbContext are scoped; the bypass is scoped so it never leaks).
        ITenantBypass tenantBypass = scope.ServiceProvider.GetRequiredService<ITenantBypass>();
        OutboxDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();

        // Processing the Outbox is legitimate cross-tenant system work: open an auditable bypass so
        // the global company_id query filter is lifted for this unit of work only.
        using IDisposable _ = tenantBypass.BeginScope(BypassReason);

        await dispatcher.ProcessPendingAsync(_options.BatchSize, _options.MaxAttempts, cancellationToken);
    }
}
