namespace SISLAB.Modules.Agenda.Domain.Subscriptions;

/// <summary>
/// Aggregate repository for <see cref="IcalSubscription"/> (card [E10.10]). Interface in the Domain, EF
/// implementation in the Infrastructure (Dependency Inversion). The public <c>.ics</c> feed does not load the
/// aggregate through this repository — it resolves the token via a tenant-explicit Dapper read instead, because
/// the feed request has no authenticated tenant context.
/// </summary>
public interface IIcalSubscriptionRepository
{
    /// <summary>Loads the active company's subscription for the given user, or <see langword="null"/>.</summary>
    Task<IcalSubscription?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Stages a new subscription for insertion on the next unit-of-work commit.</summary>
    void Add(IcalSubscription subscription);
}
