using SISLAB.Modules.Identity.Application.Administration;
using SISLAB.Modules.Identity.Domain.Companies;

namespace SISLAB.Modules.Identity.Tests.Administration;

/// <summary>
/// Proves the cross-tenant enumeration handler (E6, Fork #2 → E1) that the background alert jobs use to
/// iterate every company: it returns the ids of the active companies (via
/// <see cref="ICompanyRepository.ListActiveAsync"/>) and nothing else, so a deactivated tenant is never scanned.
/// </summary>
public sealed class ListAllCompanyIdsQueryHandlerTests
{
    [Fact]
    public async Task Returns_the_ids_of_every_active_company()
    {
        Company a = Company.Seed(new("10000000-0000-0000-0000-00000000000a"), "LAFTE");
        Company b = Company.Seed(new("20000000-0000-0000-0000-00000000000b"), "Acme Lab");

        var handler = new ListAllCompanyIdsQueryHandler(new ActiveCompaniesStub([a, b]));

        ListAllCompanyIdsQueryResult result = await handler.HandleAsync(new ListAllCompanyIdsQuery());

        Assert.Equal(2, result.CompanyIds.Count);
        Assert.Contains(a.Id, result.CompanyIds);
        Assert.Contains(b.Id, result.CompanyIds);
    }

    [Fact]
    public async Task Returns_an_empty_list_when_there_are_no_active_companies()
    {
        var handler = new ListAllCompanyIdsQueryHandler(new ActiveCompaniesStub([]));

        ListAllCompanyIdsQueryResult result = await handler.HandleAsync(new ListAllCompanyIdsQuery());

        Assert.Empty(result.CompanyIds);
    }

    /// <summary>Stub that only implements <see cref="ICompanyRepository.ListActiveAsync"/> — the sole method this handler uses.</summary>
    private sealed class ActiveCompaniesStub : ICompanyRepository
    {
        private readonly IReadOnlyList<Company> _active;

        public ActiveCompaniesStub(IReadOnlyList<Company> active) => _active = active;

        public Task<IReadOnlyList<Company>> ListActiveAsync(CancellationToken ct = default)
            => Task.FromResult(_active);

        public Task<Company?> FindByIdAsync(Guid id, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Company>> ListForMemberAsync(Guid lumenUserId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<bool> IsActiveMemberAsync(Guid companyId, Guid lumenUserId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task AddAsync(Company company, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task UpdateAsync(Company company, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
