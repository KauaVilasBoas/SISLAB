using SISLAB.Modules.Identity.Domain.Companies;

namespace SISLAB.Modules.Identity.Tests.Administration;

/// <summary>
/// Minimal <see cref="ICompanyRepository"/> stub for admin read-handler tests:
/// returns the seeded company only for its own id, and null otherwise.
/// Write and cross-membership methods are out of scope for these read handlers.
/// </summary>
internal sealed class CompanyRepositoryStub : ICompanyRepository
{
    private readonly Company? _company;

    public CompanyRepositoryStub(Company? company) => _company = company;

    public Task<Company?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_company is not null && _company.Id == id ? _company : null);

    public Task<IReadOnlyList<Company>> ListActiveAsync(CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<Company>> ListForMemberAsync(Guid lumenUserId, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<bool> IsActiveMemberAsync(Guid companyId, Guid lumenUserId, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task AddAsync(Company company, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task UpdateAsync(Company company, CancellationToken ct = default)
        => throw new NotSupportedException();
}
