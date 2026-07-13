using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Notifications.Infrastructure.Messaging;

/// <summary>
/// A domain-event dispatcher for the Notifications module that clears events without any Outbox involvement
/// (Option A — a notification is the terminal effect of an alert, not an event to propagate). It exists so the
/// shared <see cref="SISLAB.Infrastructure.Persistence.EfUnitOfWork{TContext}"/> can run for the write-side
/// commands of this module (the <c>MarkNotificationAsRead</c> command) without the Outbox writer / outbox
/// table that <see cref="SISLAB.Infrastructure.Messaging.DomainEventDispatcher"/> requires.
/// </summary>
/// <remarks>
/// Transactional handlers are intentionally NOT invoked: this module has no cross-context business invariant
/// to enforce in-transaction. Should a future in-context reaction be needed (e.g. push/e-mail fan-out on
/// <c>NotificationRaisedEvent</c>), swap this for the real dispatcher plus an outbox — the aggregate already
/// raises the events, so no domain change is required.
/// </remarks>
internal sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchTransactionalAsync(
        IEnumerable<IHasDomainEvents> aggregates,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DispatchToOutboxAsync(
        IEnumerable<IHasDomainEvents> aggregates,
        CancellationToken cancellationToken = default)
    {
        // Events are not propagated anywhere; drain them so the aggregates do not re-report on the next save.
        foreach (IHasDomainEvents aggregate in aggregates)
            aggregate.ClearDomainEvents();

        return Task.CompletedTask;
    }
}
