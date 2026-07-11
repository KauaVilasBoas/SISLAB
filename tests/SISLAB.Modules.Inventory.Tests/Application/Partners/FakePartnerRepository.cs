using SISLAB.Modules.Inventory.Domain.Partners;

namespace SISLAB.Modules.Inventory.Tests.Application.Partners;

/// <summary>
/// In-memory <see cref="IPartnerRepository"/> test double. Records the last aggregate handed back for
/// persistence so handler tests can assert the save-side wiring without a database.
/// </summary>
internal sealed class FakePartnerRepository : IPartnerRepository
{
    private readonly Dictionary<Guid, Partner> _partners = new();

    public Partner? LastAdded { get; private set; }

    public Partner? LastUpdated { get; private set; }

    public FakePartnerRepository Seed(Partner partner)
    {
        _partners[partner.Id] = partner;
        return this;
    }

    public Task<Partner?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_partners.GetValueOrDefault(id));

    public Task AddAsync(Partner partner, CancellationToken ct = default)
    {
        _partners[partner.Id] = partner;
        LastAdded = partner;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Partner partner, CancellationToken ct = default)
    {
        _partners[partner.Id] = partner;
        LastUpdated = partner;
        return Task.CompletedTask;
    }
}
