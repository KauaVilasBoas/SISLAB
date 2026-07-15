using SISLAB.Modules.Identity.Domain.Companies;

namespace SISLAB.Modules.Identity.Tests.Invitations;

/// <summary>
/// In-memory <see cref="ICompanyRepository"/> for invitation-handler tests: holds one or more companies and
/// records Update calls, so the accept flow's membership addition can be asserted. Only the members used by
/// these handlers are implemented.
/// </summary>
internal sealed class FakeCompanyRepository : ICompanyRepository
{
    private readonly Dictionary<Guid, Company> _companies = [];

    public List<Company> Updated { get; } = [];

    public FakeCompanyRepository(params Company[] companies)
    {
        foreach (Company company in companies)
            _companies[company.Id] = company;
    }

    public Task<Company?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_companies.GetValueOrDefault(id));

    public Task UpdateAsync(Company company, CancellationToken ct = default)
    {
        _companies[company.Id] = company;
        Updated.Add(company);
        return Task.CompletedTask;
    }

    public Task AddAsync(Company company, CancellationToken ct = default)
    {
        _companies[company.Id] = company;
        return Task.CompletedTask;
    }

    public Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<Company>> ListActiveAsync(CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<Company>> ListForMemberAsync(Guid lumenUserId, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<bool> IsActiveMemberAsync(Guid companyId, Guid lumenUserId, CancellationToken ct = default)
        => throw new NotSupportedException();
}
