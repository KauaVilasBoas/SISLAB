using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Infrastructure.Messaging;

/// <summary>
/// Domain-event dispatcher for the Configuration module that clears events without any Outbox involvement.
/// The per-tenant configuration aggregates raise no integration events (changing a category or the expiry
/// window is a local setting, not a cross-module fact to propagate), so this exists only so the shared
/// <see cref="SISLAB.Infrastructure.Persistence.EfUnitOfWork{TContext}"/> can run for this module's write-side
/// commands without the Outbox writer / outbox table the real dispatcher requires (same shape as the
/// Notifications module's no-op dispatcher).
/// </summary>
internal sealed class NoOpConfigurationDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchTransactionalAsync(
        IEnumerable<IHasDomainEvents> aggregates,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DispatchToOutboxAsync(
        IEnumerable<IHasDomainEvents> aggregates,
        CancellationToken cancellationToken = default)
    {
        // Events are not propagated anywhere; drain them so aggregates do not re-report on the next save.
        foreach (IHasDomainEvents aggregate in aggregates)
            aggregate.ClearDomainEvents();

        return Task.CompletedTask;
    }
}
