using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Agenda.Domain.Subscriptions;
using SISLAB.Modules.Agenda.Infrastructure.Persistence;

namespace SISLAB.Modules.Agenda.Infrastructure.Repositories;

internal sealed class IcalSubscriptionRepository : IIcalSubscriptionRepository
{
    private readonly AgendaDbContext _db;

    public IcalSubscriptionRepository(AgendaDbContext db) => _db = db;

    public Task<IcalSubscription?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => _db.IcalSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

    public void Add(IcalSubscription subscription) => _db.IcalSubscriptions.Add(subscription);
}
