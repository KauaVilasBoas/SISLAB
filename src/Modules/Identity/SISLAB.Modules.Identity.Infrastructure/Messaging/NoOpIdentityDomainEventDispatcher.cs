using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Infrastructure.Messaging;

/// <summary>
/// Domain-event dispatcher for the Identity module that drains aggregate events without any Outbox
/// involvement (same shape as the Configuration/Notifications no-op dispatchers). It exists so the shared
/// <see cref="SISLAB.Infrastructure.Persistence.EfUnitOfWork{TContext}"/> can run for this module's write-side
/// commands (the signup command) without the Outbox writer / outbox table the real dispatcher requires.
/// </summary>
/// <remarks>
/// <c>CompanyCreated</c> is raised by the aggregate but has no cross-module consumer yet: the tenant
/// provisioning that reacts to it (card #75b — default profiles, units, categories, expiry policy) is a
/// separate card. When it lands, swap this for the real dispatcher plus a <c>CompanyCreated → IntegrationEvent</c>
/// translator and an outbox table for the Identity schema — the aggregate already raises the event, so no
/// domain change is required. Transactional handlers are intentionally not invoked: signup has no in-transaction
/// cross-context invariant to enforce here.
/// </remarks>
internal sealed class NoOpIdentityDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchTransactionalAsync(
        IEnumerable<IHasDomainEvents> aggregates,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DispatchToOutboxAsync(
        IEnumerable<IHasDomainEvents> aggregates,
        CancellationToken cancellationToken = default)
    {
        // Events are not propagated anywhere yet; drain them so aggregates do not re-report on the next save.
        foreach (IHasDomainEvents aggregate in aggregates)
            aggregate.ClearDomainEvents();

        return Task.CompletedTask;
    }
}
