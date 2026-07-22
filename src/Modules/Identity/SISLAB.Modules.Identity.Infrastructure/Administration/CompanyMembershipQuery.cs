using SISLAB.Modules.Identity.Contracts.Administration;
using SISLAB.Modules.Identity.Domain.Companies;

namespace SISLAB.Modules.Identity.Infrastructure.Administration;

/// <summary>
/// Implements the public <see cref="ICompanyMembershipQuery"/> port by delegating to the module's
/// <see cref="ICompanyRepository"/> — the single source of truth for the tenancy link (<c>company_user</c>).
/// Keeps the cross-module membership check behind the Contracts boundary: consumers (Experiments) never see the
/// repository or the Identity Domain, only this port.
/// </summary>
internal sealed class CompanyMembershipQuery : ICompanyMembershipQuery
{
    private readonly ICompanyRepository _companyRepository;

    public CompanyMembershipQuery(ICompanyRepository companyRepository)
        => _companyRepository = companyRepository;

    public Task<bool> IsActiveMemberAsync(
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _companyRepository.IsActiveMemberAsync(companyId, userId, cancellationToken);
}
