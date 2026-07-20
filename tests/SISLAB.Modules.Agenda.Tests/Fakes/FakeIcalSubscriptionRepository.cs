using SISLAB.Modules.Agenda.Domain.Subscriptions;

namespace SISLAB.Modules.Agenda.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IIcalSubscriptionRepository"/> for command-handler tests: serves a seeded subscription
/// by user and records additions, so subscribe/renew orchestration can be asserted without a database.
/// </summary>
internal sealed class FakeIcalSubscriptionRepository : IIcalSubscriptionRepository
{
    private readonly Dictionary<Guid, IcalSubscription> _byUser = new();

    public List<IcalSubscription> Added { get; } = [];

    public FakeIcalSubscriptionRepository Seed(IcalSubscription subscription)
    {
        _byUser[subscription.UserId] = subscription;
        return this;
    }

    public Task<IcalSubscription?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(_byUser.GetValueOrDefault(userId));

    public void Add(IcalSubscription subscription)
    {
        _byUser[subscription.UserId] = subscription;
        Added.Add(subscription);
    }
}
